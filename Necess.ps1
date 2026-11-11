#Requires -Version 5
<#
  Necess - Essenciais do PC, baixe tudo num lugar so.
  GUI portatil (WinForms). Baixa instaladores direto e abre paginas oficiais.
  Rode:  Necess.bat   (ou)   powershell -STA -ExecutionPolicy Bypass -File Necess.ps1
  Teste: powershell -ExecutionPolicy Bypass -File Necess.ps1 -SelfCheck
         powershell -ExecutionPolicy Bypass -File Necess.ps1 -SelfCheck -Online
  (-Online tambem testa, na internet, se cada link ainda entrega o arquivo certo)

  Nota tecnica: os handlers da GUI NAO usam GetNewClosure (closures aninhados
  perdem as variaveis e davam "A propriedade 'Text' nao foi encontrada").
  O estado de cada linha fica numa hashtable guardada em .Tag / UserState.
#>
param([switch]$SelfCheck, [switch]$Online)

$ErrorActionPreference = 'Stop'
$Root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$DownloadDir = Join-Path $Root 'Downloads'
$UA = 'Mozilla/5.0'
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

# --- Catalogo. Kind: 'download' salva o arquivo; 'page' abre no navegador. -----
# Expect: o que o arquivo baixado TEM que ser (conferido pelos primeiros bytes,
#         para nunca salvar uma pagina de erro HTML com nome de .exe/.zip):
#         exe | zip | archive (zip, rar ou 7z - a extensao e acertada pelo conteudo)
$Items = @(
  @{ Name='Visual C++ Runtimes All-in-One'; Tag='Jul 2025 - TechPowerUp'
     Desc='Pacote com todos os runtimes do Visual C++. Abre a pagina para escolher o mirror.'
     Kind='page'; Url='https://www.techpowerup.com/download/visual-c-redistributable-runtime-package-all-in-one/' }
  @{ Name='Visual C++ Redistributable x64'; Tag='Microsoft (oficial)'
     Desc='Runtime VC++ mais recente, 64 bits.'
     Kind='download'; Expect='exe'; File='VC_redist.x64.exe'; Url='https://aka.ms/vs/17/release/vc_redist.x64.exe' }
  @{ Name='DirectX End-User Runtime'; Tag='Microsoft (oficial)'
     Desc='Instalador web do DirectX (dxwebsetup).'
     Kind='download'; Expect='exe'; File='dxwebsetup.exe'; Url='https://download.microsoft.com/download/1/7/1/1718ccc4-6315-4d8e-9543-8e28a4e18c4c/dxwebsetup.exe' }
  @{ Name='WinRAR'; Tag='RARLAB (oficial) - 64 bits, portugues do Brasil'
     Desc='Abre arquivos .rar e .zip. Precisa dele para abrir o Defender Control.'
     Kind='download'; Expect='exe'; File='WinRAR_x64.exe'
     Url='https://www.rarlab.com/rar/winrar-x64-723br.exe'
     LatestPage='https://www.rarlab.com/download.htm'
     LatestPattern='href="(/rar/winrar-x64-\d+br\.exe)"'
     LatestBase='https://www.rarlab.com' }
  @{ Name='Windows Update Blocker'; Tag='Sordum - Wub'
     Desc='Liga/desliga o Windows Update. Portatil, sem instalacao.'
     Kind='download'; Expect='archive'; File='Wub.zip'; Url='https://www.sordum.org/files/downloads.php?st-windows-update-blocker' }
  @{ Name='Defender Control'; Tag='Sordum - dControl'
     Desc='Liga/desliga o Windows Defender. Vem em .rar com senha: sordum (use o WinRAR).'
     Kind='download'; Expect='archive'; File='dControl.rar'; Url='https://www.sordum.org/files/downloads.php?st-defender-control' }
  @{ Name='Valorant / Riot Client'; Tag='Riot Games (oficial)'
     Desc='So abre a pagina oficial para voce baixar o launcher da Riot.'
     Kind='page'; Url='https://playvalorant.com/pt-br/download/' }
)

# Pacote do proprio Necess. HEAD = branch padrao do repositorio (nao quebra se o
# nome do branch mudar).
$ZipDef = @{ Name='Necess.zip'; Kind='download'; Expect='zip'; File='Necess.zip'
             Url='https://raw.githubusercontent.com/shazamfa90/PROJETINHO/HEAD/Necess.zip' }

# --- Utilitarios (sem GUI) -----------------------------------------------------
function Write-Log([string]$Text) {
  try {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Text
    Add-Content -LiteralPath (Join-Path $script:Root 'Necess.log') -Value $line
  } catch {}
}

# Descobre o tipo real do arquivo pelos primeiros bytes.
function Get-KindFromBytes([byte[]]$B, [int]$N) {
  if ($N -ge 2 -and $B[0] -eq 0x4D -and $B[1] -eq 0x5A) { return 'exe' }   # MZ
  if ($N -ge 4 -and $B[0] -eq 0x50 -and $B[1] -eq 0x4B) { return 'zip' }   # PK
  if ($N -ge 4 -and $B[0] -eq 0x52 -and $B[1] -eq 0x61 -and $B[2] -eq 0x72 -and $B[3] -eq 0x21) { return 'rar' }   # Rar!
  if ($N -ge 4 -and $B[0] -eq 0x37 -and $B[1] -eq 0x7A -and $B[2] -eq 0xBC -and $B[3] -eq 0xAF) { return '7z' }
  if ($N -ge 1) {
    $len = [Math]::Min($N, 512)
    $t = [Text.Encoding]::ASCII.GetString($B, 0, $len)
    if ($t -match '^[^A-Za-z0-9<]{0,8}<(!doctype|html|head|body|\?xml)') { return 'html' }
  }
  return 'unknown'
}

function Get-FileKind([string]$Path) {
  try {
    $fs = [IO.File]::OpenRead($Path)
    try { $buf = New-Object byte[] 1024; $n = $fs.Read($buf, 0, 1024) } finally { $fs.Dispose() }
  } catch { return 'missing' }
  return (Get-KindFromBytes $buf $n)
}

function Test-Expect([string]$Expect, [string]$Kind) {
  switch ($Expect) {
    'exe'     { return ($Kind -eq 'exe') }
    'zip'     { return ($Kind -eq 'zip') }
    'archive' { return ($Kind -in @('zip', 'rar', '7z')) }
    default   { return $true }
  }
}

# O Google Drive nao entrega arquivos que ele nao consegue escanear (ex.: .rar com
# senha): devolve uma pagina HTML "Virus scan warning" com um formulario
# "Download anyway". Esta funcao le o formulario e monta a URL de confirmacao.
# Retorna $null se o HTML nao for essa pagina.
function Get-DriveConfirmUrl([string]$Html) {
  $act = [regex]::Match($Html, '<form[^>]*id="download-form"[^>]*action="([^"]+)"')
  if (-not $act.Success) { return $null }
  $base = [Net.WebUtility]::HtmlDecode($act.Groups[1].Value)
  if ($base -notmatch '^https://(drive\.usercontent\.google\.com|drive\.google\.com|docs\.google\.com)/') { return $null }
  $pairs = New-Object Collections.ArrayList
  foreach ($m in [regex]::Matches($Html, '<input[^>]*type="hidden"[^>]*name="([^"]+)"[^>]*value="([^"]*)"')) {
    $val = [Uri]::EscapeDataString([Net.WebUtility]::HtmlDecode($m.Groups[2].Value))
    [void]$pairs.Add(('{0}={1}' -f $m.Groups[1].Value, $val))
  }
  if ($pairs.Count -eq 0) { return $null }
  return ($base + '?' + ($pairs -join '&'))
}

function Get-WebText([string]$Url, [int]$TimeoutMs = 8000) {
  $req = [Net.HttpWebRequest]::Create($Url)
  $req.UserAgent = $script:UA
  $req.Timeout = $TimeoutMs
  $req.ReadWriteTimeout = $TimeoutMs
  $resp = $req.GetResponse()
  try {
    $sr = New-Object IO.StreamReader($resp.GetResponseStream())
    try { return $sr.ReadToEnd() } finally { $sr.Dispose() }
  } finally { $resp.Close() }
}

# Itens com LatestPage (ex.: WinRAR) procuram a versao mais nova no site oficial;
# se falhar, usam a URL fixa do catalogo.
function Get-LatestUrl($Def) {
  if (-not $Def.LatestPage) { return $Def.Url }
  try {
    $html = Get-WebText $Def.LatestPage
    $m = [regex]::Match($html, $Def.LatestPattern)
    if ($m.Success) { return ($Def.LatestBase + $m.Groups[1].Value) }
  } catch { Write-Log ('latest {0}: {1}' -f $Def.Name, $_.Exception.Message) }
  return $Def.Url
}

# --- Self-check: valida o catalogo e sai (sem GUI). ----------------------------
function Test-Catalog {
  $errs = @()
  foreach ($i in $Items) {
    if (-not $i.Name)                         { $errs += 'item sem Name' }
    if ($i.Kind -notin 'download','page')     { $errs += "$($i.Name): Kind invalido" }
    if ([string]::IsNullOrWhiteSpace($i.Url)) { $errs += "$($i.Name): Url vazia" }
    if ($i.Url -notmatch '^https://')         { $errs += "$($i.Name): Url nao e https" }
    if ($i.Kind -eq 'download') {
      if ([string]::IsNullOrWhiteSpace($i.File)) { $errs += "$($i.Name): download sem File" }
      if ($i.Expect -notin 'exe','zip','archive') { $errs += "$($i.Name): Expect invalido" }
    }
  }
  if ($errs) { $errs | ForEach-Object { Write-Host "FALHOU: $_" -ForegroundColor Red }; return $false }
  Write-Host "OK: $($Items.Count) itens validos." -ForegroundColor Green; return $true
}

function Read-Head([string]$Url, [int]$Max = 4096) {
  $req = [Net.HttpWebRequest]::Create($Url)
  $req.UserAgent = $script:UA
  $req.Timeout = 20000
  $req.ReadWriteTimeout = 20000
  $resp = $req.GetResponse()
  try {
    $st = $resp.GetResponseStream()
    $buf = New-Object byte[] $Max
    $n = 0
    while ($n -lt $Max) {
      $r = $st.Read($buf, $n, $Max - $n)
      if ($r -le 0) { break }
      $n += $r
    }
    $out = New-Object byte[] $n
    [Array]::Copy($buf, $out, $n)
    return ,$out
  } finally { $resp.Close() }
}

function Get-RemoteKind([string]$Url) {
  $b = Read-Head $Url 4096
  $k = Get-KindFromBytes $b $b.Length
  if ($k -eq 'html') {
    $next = Get-DriveConfirmUrl ([Text.Encoding]::UTF8.GetString($b))
    if ($next) { $b = Read-Head $next 4096; $k = Get-KindFromBytes $b $b.Length }
  }
  return $k
}

function Test-Online {
  $good = $true
  $all = @($Items) + @($ZipDef)
  foreach ($i in $all) {
    if ($i.Kind -ne 'download') { continue }
    try {
      $url = Get-LatestUrl $i
      $kind = Get-RemoteKind $url
      if (Test-Expect $i.Expect $kind) {
        Write-Host ('OK     {0}: {1}  <- {2}' -f $i.Name, $kind, $url) -ForegroundColor Green
      } else {
        Write-Host ('FALHOU {0}: veio "{1}", esperado "{2}"  <- {3}' -f $i.Name, $kind, $i.Expect, $url) -ForegroundColor Red
        $good = $false
      }
    } catch {
      Write-Host ('FALHOU {0}: {1}' -f $i.Name, $_.Exception.Message) -ForegroundColor Red
      $good = $false
    }
  }
  return $good
}

if ($SelfCheck) {
  $okCat = Test-Catalog
  $okNet = $true
  if ($Online) { $okNet = Test-Online }
  exit ([int](-not ($okCat -and $okNet)))
}

# --- GUI -----------------------------------------------------------------------
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

# Em vez do dialogo "Ocorreu uma excecao sem tratamento", registra no Necess.log.
try {
  [System.Windows.Forms.Application]::SetUnhandledExceptionMode([System.Windows.Forms.UnhandledExceptionMode]::CatchException)
  [System.Windows.Forms.Application]::add_ThreadException({
    param($s, $e)
    Write-Log ('excecao: ' + $e.Exception.ToString())
    try { $script:foot.ForeColor = $script:cErr; $script:foot.Text = 'Erro: ' + $e.Exception.Message } catch {}
  })
} catch { Write-Log ('SetUnhandledExceptionMode: ' + $_.Exception.Message) }

# paleta (slate profundo, um azul eletrico de acento)
$cBg     = [Drawing.Color]::FromArgb(15,20,27)
$cCard   = [Drawing.Color]::FromArgb(23,30,39)
$cCard2  = [Drawing.Color]::FromArgb(30,39,51)
$cLine   = [Drawing.Color]::FromArgb(40,51,66)
$cText   = [Drawing.Color]::FromArgb(230,234,240)
$cMuted  = [Drawing.Color]::FromArgb(138,148,166)
$cAccent = [Drawing.Color]::FromArgb(76,141,255)
$cAccTxt = [Drawing.Color]::White
$cOk     = [Drawing.Color]::FromArgb(55,201,120)
$cErr    = [Drawing.Color]::FromArgb(242,85,90)
$fName   = New-Object Drawing.Font('Segoe UI',11,[Drawing.FontStyle]::Bold)
$fDesc   = New-Object Drawing.Font('Segoe UI',8.5)
$fTag    = New-Object Drawing.Font('Segoe UI Semibold',8)
$fBtn    = New-Object Drawing.Font('Segoe UI',9.5,[Drawing.FontStyle]::Bold)

# Estado global dos downloads (lido/alterado pelos handlers via $script:).
$Rows      = New-Object Collections.ArrayList   # linhas de download (para "Baixar tudo")
$Active    = 0                                  # downloads em andamento
$BatchOk   = New-Object Collections.ArrayList   # concluidos neste lote
$BatchFail = New-Object Collections.ArrayList   # falhos neste lote
$LastPath  = $null

# --- Funcoes da GUI (chamadas pelos handlers) ----------------------------------
function Set-Status($Ctx, [string]$Text, [Drawing.Color]$Color) {
  $Ctx.StatusLbl.ForeColor = $Color
  $Ctx.StatusLbl.Text = $Ctx.Prefix + $Text
}

function Open-InFolder([string]$Path) {
  if ($Path -and (Test-Path -LiteralPath $Path)) {
    Start-Process explorer.exe -ArgumentList ('/select,"{0}"' -f $Path)
  } else {
    New-Item -ItemType Directory -Force -Path $script:DownloadDir | Out-Null
    Start-Process explorer.exe -ArgumentList ('"{0}"' -f $script:DownloadDir)
  }
}

# Notificacao do Windows (balao/toast na area de notificacao).
function Show-Toast([string]$Title, [string]$Msg, [string]$Icon = 'Info') {
  try {
    $script:tray.ShowBalloonTip(8000, $Title, $Msg, [System.Windows.Forms.ToolTipIcon]$Icon)
  } catch { Write-Log ('toast: ' + $_.Exception.Message) }
}

# Quando nao ha mais downloads em andamento: notifica e abre a pasta (uma vez so,
# mesmo no "Baixar tudo").
function Complete-Batch {
  if ($script:Active -gt 0) { return }
  try {
    $ok  = @($script:BatchOk.ToArray())
    $bad = @($script:BatchFail.ToArray())
    $script:BatchOk.Clear()
    $script:BatchFail.Clear()
    if ($ok.Count -eq 0 -and $bad.Count -eq 0) { return }
    $badNames = ($bad | ForEach-Object { $_.Def.Name }) -join ', '
    if ($ok.Count -eq 0) {
      Show-Toast 'Necess - falhou' ('Nao foi possivel baixar: ' + $badNames) 'Error'
      return
    }
    if ($ok.Count -eq 1) {
      $script:LastPath = $ok[0].Path
      $msg = '{0} baixado. Abrindo a pasta...' -f $ok[0].Def.Name
    } else {
      $script:LastPath = $null
      $msg = '{0} arquivos baixados. Abrindo a pasta...' -f $ok.Count
    }
    if ($bad.Count -gt 0) { $msg += ' Falhou: ' + $badNames }
    Show-Toast 'Necess - download concluido' $msg 'Info'
    Open-InFolder $script:LastPath
  } catch { Write-Log ('lote: ' + $_.Exception.ToString()) }
}

function Set-DownloadFailed($Ctx, [string]$Text) {
  if (-not $Ctx.Busy) { return }
  if ($Ctx.Temp -and (Test-Path -LiteralPath $Ctx.Temp)) { Remove-Item -LiteralPath $Ctx.Temp -Force -ErrorAction SilentlyContinue }
  $Ctx.Busy = $false
  Set-Status $Ctx $Text $script:cErr
  $Ctx.Button.Enabled = $true
  $Ctx.Button.BackColor = $script:cAccent
  $Ctx.Button.ForeColor = $script:cAccTxt
  $Ctx.Button.Text = $Ctx.OrigText
  [void]$script:BatchFail.Add($Ctx)
  if ($script:Active -gt 0) { $script:Active-- }
  Complete-Batch
}

function New-Client {
  $wc = New-Object Net.WebClient
  $wc.Headers.Add('User-Agent', $script:UA)
  $wc.add_DownloadProgressChanged($script:onProgress)
  $wc.add_DownloadFileCompleted($script:onCompleted)
  return $wc
}

function Start-RowDownload($Ctx) {
  if ($Ctx.Busy) { return }
  $def = $Ctx.Def
  $Ctx.Busy = $true; $Ctx.Done = $false; $Ctx.Retried = $false; $Ctx.Path = $null
  $Ctx.Button.Enabled = $false
  $Ctx.Button.BackColor = $script:cCard2
  Set-Status $Ctx 'Preparando...' $script:cMuted
  $Ctx.StatusLbl.Refresh()
  $script:Active++
  try {
    New-Item -ItemType Directory -Force -Path $script:DownloadDir | Out-Null
    # baixa para .part e so renomeia depois de conferir o conteudo
    $Ctx.Temp = Join-Path $script:DownloadDir ($def.File + '.part')
    $url = Get-LatestUrl $def
    $Ctx.Client = New-Client
    Set-Status $Ctx 'Baixando...' $script:cMuted
    $Ctx.Client.DownloadFileAsync([Uri]$url, $Ctx.Temp, $Ctx)
  } catch {
    Write-Log ('{0}: iniciar: {1}' -f $def.Name, $_.Exception.Message)
    Set-DownloadFailed $Ctx 'Nao deu para iniciar - tentar de novo'
  }
}

function Complete-Download($Ctx, $Evt) {
  $def = $Ctx.Def
  if ($Evt.Cancelled) { Set-DownloadFailed $Ctx 'Cancelado - tentar de novo'; return }
  if ($Evt.Error) {
    Write-Log ('{0}: {1}' -f $def.Name, $Evt.Error.Message)
    Set-DownloadFailed $Ctx 'Falhou - tentar de novo'
    return
  }

  $kind = Get-FileKind $Ctx.Temp

  # Google Drive devolveu a pagina de "Virus scan warning": segue o "Download anyway".
  if ($kind -eq 'html' -and -not $Ctx.Retried) {
    $next = $null
    try { $next = Get-DriveConfirmUrl ([IO.File]::ReadAllText($Ctx.Temp)) } catch {}
    if ($next) {
      $Ctx.Retried = $true
      Remove-Item -LiteralPath $Ctx.Temp -Force -ErrorAction SilentlyContinue
      $Ctx.Client = New-Client
      Set-Status $Ctx 'Baixando...' $script:cMuted
      $Ctx.Client.DownloadFileAsync([Uri]$next, $Ctx.Temp, $Ctx)
      return
    }
  }

  if (-not (Test-Expect $def.Expect $kind)) {
    Write-Log ('{0}: conteudo inesperado "{1}" (esperado "{2}")' -f $def.Name, $kind, $def.Expect)
    Set-DownloadFailed $Ctx 'Arquivo invalido - tentar de novo'
    return
  }

  # nome final (archive: a extensao vem do conteudo real - .rar, .zip ou .7z)
  if ($def.Expect -eq 'archive') {
    $final = Join-Path $script:DownloadDir ([IO.Path]::GetFileNameWithoutExtension($def.File) + '.' + $kind)
  } else {
    $final = Join-Path $script:DownloadDir $def.File
  }
  Move-Item -LiteralPath $Ctx.Temp -Destination $final -Force
  $Ctx.Path = $final
  $Ctx.Busy = $false
  $Ctx.Done = $true
  Set-Status $Ctx 'Concluido' $script:cOk
  $Ctx.Button.Text = 'Abrir pasta'
  $Ctx.Button.Enabled = $true
  $Ctx.Button.BackColor = $script:cCard2
  $Ctx.Button.ForeColor = $script:cText
  [void]$script:BatchOk.Add($Ctx)
  if ($script:Active -gt 0) { $script:Active-- }
  Complete-Batch
}

# --- Handlers (blocos simples, sem closure; o estado vem de .Tag / UserState) ---
$onProgress = {
  param($sender, $e)
  try {
    $ctx = $e.UserState
    if ($null -eq $ctx) { return }
    if ($e.TotalBytesToReceive -gt 0) { $t = 'Baixando {0}%' -f $e.ProgressPercentage }
    else { $t = 'Baixando {0:N1} MB' -f ($e.BytesReceived / 1MB) }
    Set-Status $ctx $t $script:cMuted
  } catch { Write-Log ('progresso: ' + $_.Exception.Message) }
}

$onCompleted = {
  param($sender, $e)
  $ctx = $e.UserState
  try { Complete-Download $ctx $e }
  catch {
    Write-Log ('concluir: ' + $_.Exception.ToString())
    try { Set-DownloadFailed $ctx 'Falhou - tentar de novo' } catch {}
  }
}

$onRowClick = {
  param($sender, $e)
  $ctx = $sender.Tag
  try {
    if ($ctx.Def.Kind -eq 'page') {
      try { Start-Process $ctx.Def.Url } catch { Set-Status $ctx 'nao abriu' $script:cErr }
      return
    }
    if ($ctx.Done) { Open-InFolder $ctx.Path; return }
    Start-RowDownload $ctx
  } catch { Write-Log ('clique: ' + $_.Exception.ToString()) }
}

$onAll = {
  param($sender, $e)
  try {
    $script:Active++      # segura o "fim do lote" ate terminar de iniciar todos
    foreach ($c in @($script:Rows.ToArray())) {
      if (-not $c.Busy -and -not $c.Done) { Start-RowDownload $c }
    }
  } catch { Write-Log ('baixar tudo: ' + $_.Exception.ToString()) }
  finally {
    if ($script:Active -gt 0) { $script:Active-- }
    Complete-Batch
  }
}

$onOpenDir = {
  param($sender, $e)
  try { Open-InFolder $null } catch { Write-Log ('abrir pasta: ' + $_.Exception.Message) }
}

# --- Janela --------------------------------------------------------------------
try {
  $form = New-Object Windows.Forms.Form
  $form.Text = 'Necess'
  $form.StartPosition = 'CenterScreen'
  $form.FormBorderStyle = 'FixedSingle'
  $form.MaximizeBox = $false
  $form.BackColor = $cBg
  $form.Font = New-Object Drawing.Font('Segoe UI',9)
  $ico = Join-Path $Root 'Necess.ico'
  if (Test-Path $ico) { try { $form.Icon = New-Object Drawing.Icon($ico) } catch {} }

  # icone na area de notificacao (usado pelas notificacoes)
  $tray = New-Object System.Windows.Forms.NotifyIcon
  $tray.Icon = if ($form.Icon) { $form.Icon } else { [System.Drawing.SystemIcons]::Information }
  $tray.Text = 'Necess'
  $tray.Visible = $true
  $tray.add_BalloonTipClicked({ try { Open-InFolder $script:LastPath } catch {} })
  $form.Add_FormClosed({ try { $script:tray.Visible = $false; $script:tray.Dispose() } catch {} })

  # cabecalho
  $logo = New-Object Windows.Forms.PictureBox
  $logo.Size = New-Object Drawing.Size(52,52)
  $logo.Location = New-Object Drawing.Point(24,22)
  $logo.SizeMode = 'Zoom'
  $png = Join-Path $Root 'Necess.png'
  if (Test-Path $png) { try { $logo.Image = [Drawing.Image]::FromFile($png) } catch {} }
  $form.Controls.Add($logo)

  $title = New-Object Windows.Forms.Label
  $title.Text = 'Necess'
  $title.Font = New-Object Drawing.Font('Segoe UI',20,[Drawing.FontStyle]::Bold)
  $title.ForeColor = $cText
  $title.AutoSize = $true
  $title.Location = New-Object Drawing.Point(86,24)
  $form.Controls.Add($title)

  $sub = New-Object Windows.Forms.Label
  $sub.Text = 'Essenciais do PC - baixe tudo num lugar so'
  $sub.Font = $fDesc
  $sub.ForeColor = $cMuted
  $sub.AutoSize = $true
  $sub.Location = New-Object Drawing.Point(88,60)
  $form.Controls.Add($sub)

  # helper: botao flat
  function New-Btn([string]$text,[Drawing.Color]$back,[Drawing.Color]$fore) {
    $b = New-Object Windows.Forms.Button
    $b.Text = $text; $b.Font = $fBtn
    $b.FlatStyle = 'Flat'; $b.FlatAppearance.BorderSize = 0
    $b.BackColor = $back; $b.ForeColor = $fore
    $b.Cursor = 'Hand'; $b.TabStop = $false
    return $b
  }

  $rowX = 24; $rowW = 788; $rowH = 78; $y0 = 100; $gap = 8

  function New-Row($def, [int]$top) {
    $card = New-Object Windows.Forms.Panel
    $card.Size = New-Object Drawing.Size($rowW,$rowH)
    $card.Location = New-Object Drawing.Point($rowX,$top)
    $card.BackColor = $cCard
    $form.Controls.Add($card)

    # faixa de acento a esquerda
    $stripe = New-Object Windows.Forms.Panel
    $stripe.Size = New-Object Drawing.Size(4,$rowH)
    $stripe.Location = New-Object Drawing.Point(0,0)
    $stripe.BackColor = $cAccent
    $card.Controls.Add($stripe)

    $name = New-Object Windows.Forms.Label
    $name.Text = $def.Name; $name.Font = $fName; $name.ForeColor = $cText
    $name.AutoSize = $true; $name.Location = New-Object Drawing.Point(20,10)
    $card.Controls.Add($name)

    $tag = New-Object Windows.Forms.Label
    $tag.Text = $def.Tag; $tag.Font = $fTag; $tag.ForeColor = $cAccent
    $tag.AutoSize = $true; $tag.Location = New-Object Drawing.Point(22,35)
    $card.Controls.Add($tag)

    $desc = New-Object Windows.Forms.Label
    $desc.Text = $def.Desc; $desc.Font = $fDesc; $desc.ForeColor = $cMuted
    $desc.AutoSize = $true; $desc.MaximumSize = New-Object Drawing.Size(520,0)
    $desc.Location = New-Object Drawing.Point(22,54)
    $card.Controls.Add($desc)

    $status = New-Object Windows.Forms.Label
    $status.Text = ''; $status.Font = $fTag; $status.ForeColor = $cMuted
    $status.TextAlign = 'MiddleRight'
    $status.Size = New-Object Drawing.Size(150,20)
    $status.Location = New-Object Drawing.Point(($rowW-320),29)
    $card.Controls.Add($status)

    if ($def.Kind -eq 'page') { $btnText = 'Abrir pagina'; $back = $cCard2; $fore = $cText }
    else                      { $btnText = 'Baixar';       $back = $cAccent; $fore = $cAccTxt }
    $btn = New-Btn $btnText $back $fore
    $btn.Size = New-Object Drawing.Size(150,38)
    $btn.Location = New-Object Drawing.Point(($rowW-170),20)
    $card.Controls.Add($btn)

    # todo o estado da linha numa hashtable (vai em $btn.Tag e no UserState do download)
    $ctx = @{ Def=$def; Button=$btn; StatusLbl=$status; Client=$null
              Busy=$false; Done=$false; Retried=$false; Path=$null; Temp=$null
              Prefix=''; OrigText=$btnText }
    $btn.Tag = $ctx
    $btn.Add_Click($onRowClick)
    if ($def.Kind -eq 'download') { [void]$Rows.Add($ctx) }
  }

  $top = $y0
  foreach ($it in $Items) { New-Row $it $top; $top += $rowH + $gap }

  # rodape
  $foot = New-Object Windows.Forms.Label
  $foot.Text = 'Salva em:  .\Downloads'
  $foot.Font = $fDesc; $foot.ForeColor = $cMuted; $foot.AutoSize = $true
  $foot.Location = New-Object Drawing.Point(26,($top+52))
  $form.Controls.Add($foot)

  # botao: baixa o proprio Necess.zip do repositorio (GitHub raw)
  $zipBtn = New-Btn 'Baixar Necess.zip' $cCard2 $cText
  $zipBtn.Size = New-Object Drawing.Size(180,40)
  $zipBtn.Location = New-Object Drawing.Point($rowX,($top+6))
  $form.Controls.Add($zipBtn)
  $zipCtx = @{ Def=$ZipDef; Button=$zipBtn; StatusLbl=$foot; Client=$null
               Busy=$false; Done=$false; Retried=$false; Path=$null; Temp=$null
               Prefix='Necess.zip: '; OrigText='Baixar Necess.zip' }
  $zipBtn.Tag = $zipCtx
  $zipBtn.Add_Click($onRowClick)

  $allBtn = New-Btn 'Baixar tudo' $cAccent $cAccTxt
  $allBtn.Size = New-Object Drawing.Size(160,40)
  $allBtn.Location = New-Object Drawing.Point(($rowX+$rowW-330),($top+6))
  $form.Controls.Add($allBtn)
  $allBtn.Add_Click($onAll)

  $openBtn = New-Btn 'Abrir pasta' $cCard2 $cText
  $openBtn.Size = New-Object Drawing.Size(150,40)
  $openBtn.Location = New-Object Drawing.Point(($rowX+$rowW-160),($top+6))
  $form.Controls.Add($openBtn)
  $openBtn.Add_Click($onOpenDir)

  # tamanho: cabe tudo, ou rola se a tela for pequena
  $formW = $rowX * 2 + $rowW
  $contentH = $top + 80
  $maxH = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Height - 60
  $form.AutoScroll = $true
  if ($contentH -gt $maxH) {
    $sbw = [System.Windows.Forms.SystemInformation]::VerticalScrollBarWidth
    $form.ClientSize = New-Object Drawing.Size(($formW + $sbw), $maxH)
  } else {
    $form.ClientSize = New-Object Drawing.Size($formW, $contentH)
  }

  [void]$form.ShowDialog()
}
catch {
  Write-Log ('fatal: ' + $_.Exception.ToString())
  try {
    [void][System.Windows.Forms.MessageBox]::Show(
      ("O Necess encontrou um erro:`n`n" + $_.Exception.Message + "`n`nDetalhes em Necess.log (na pasta do programa)."),
      'Necess', 'OK', 'Error')
  } catch {}
}
finally {
  try { if ($tray) { $tray.Visible = $false; $tray.Dispose() } } catch {}
}
