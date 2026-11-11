#Requires -Version 5
<#
  Necess - Essenciais do PC, baixe tudo num lugar so.
  GUI portatil (WinForms). Download sincrono e confiavel; mostra o erro se falhar.
  Rode:  Necess.bat   (ou)   powershell -STA -ExecutionPolicy Bypass -File Necess.ps1
  Teste: powershell -ExecutionPolicy Bypass -File Necess.ps1 -SelfCheck
#>
param([switch]$SelfCheck)

$ErrorActionPreference = 'Stop'
$Root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$DownloadDir = Join-Path $Root 'Downloads'
$ZipUrl = 'https://raw.githubusercontent.com/shazamfa90/PROJETINHO/ccr-7a55c30c-fx2v0g/Necess.zip'

# --- Catalogo. Kind: 'download' salva o arquivo; 'page' abre no navegador. -----
$Items = @(
  @{ Name='Visual C++ Runtimes All-in-One'; Tag='Jul 2025 - TechPowerUp'
     Desc='Pacote com todos os runtimes do Visual C++. Abre a pagina para escolher o mirror.'
     Kind='page'; Url='https://www.techpowerup.com/download/visual-c-redistributable-runtime-package-all-in-one/' }
  @{ Name='Visual C++ Redistributable x64'; Tag='Microsoft (oficial)'
     Desc='Runtime VC++ mais recente, 64 bits.'
     Kind='download'; File='VC_redist.x64.exe'; Url='https://aka.ms/vs/17/release/vc_redist.x64.exe' }
  @{ Name='DirectX End-User Runtime'; Tag='Microsoft (oficial)'
     Desc='Instalador web do DirectX (dxwebsetup).'
     Kind='download'; File='dxwebsetup.exe'; Url='https://download.microsoft.com/download/1/7/1/1718ccc4-6315-4d8e-9543-8e28a4e18c4c/dxwebsetup.exe' }
  @{ Name='WinRAR (x64)'; Tag='RARLab 7.23 - PT-BR'
     Desc='Compactador RAR/ZIP, 64 bits, para Windows 10 Pro / 11.'
     Kind='download'; File='winrar-x64-723br.exe'; Url='https://www.rarlab.com/rar/winrar-x64-723br.exe' }
  @{ Name='Windows Update Blocker'; Tag='Sordum - Wub'
     Desc='Liga/desliga o Windows Update. Portatil, sem instalacao.'
     Kind='download'; File='Wub.zip'; Url='https://www.sordum.org/files/downloads.php?st-windows-update-blocker' }
  @{ Name='Defender Control'; Tag='Sordum - dControl'
     Desc='Liga/desliga o Windows Defender (zip, senha: sordum). O proprio Defender pode bloquear este download.'
     Kind='download'; File='dControl.zip'; Url='https://www.sordum.org/files/downloads.php?st-defender-control' }
  @{ Name='Valorant / Riot Client'; Tag='Riot Games (oficial)'
     Desc='So abre a pagina oficial para voce baixar o launcher da Riot.'
     Kind='page'; Url='https://playvalorant.com/pt-br/download/' }
)

# --- Self-check: valida o catalogo e sai (sem GUI). ----------------------------
function Test-Catalog {
  $errs = @()
  foreach ($i in $Items) {
    if (-not $i.Name)                         { $errs += "item sem Name" }
    if ($i.Kind -notin 'download','page')     { $errs += "$($i.Name): Kind invalido" }
    if ([string]::IsNullOrWhiteSpace($i.Url)) { $errs += "$($i.Name): Url vazia" }
    if ($i.Url -notmatch '^https://')         { $errs += "$($i.Name): Url nao e https" }
    if ($i.Kind -eq 'download' -and [string]::IsNullOrWhiteSpace($i.File)) { $errs += "$($i.Name): download sem File" }
  }
  if ($errs) { $errs | ForEach-Object { Write-Host "FALHOU: $_" -ForegroundColor Red }; return $false }
  Write-Host "OK: $($Items.Count) itens validos." -ForegroundColor Green; return $true
}
if ($SelfCheck) { exit ([int](-not (Test-Catalog))) }

# --- GUI -----------------------------------------------------------------------
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
try { [Net.ServicePointManager]::SecurityProtocol =
        [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls11 } catch {}

# paleta (slate profundo, um azul eletrico de acento)
$cBg     = [Drawing.Color]::FromArgb(15,20,27)
$cCard   = [Drawing.Color]::FromArgb(23,30,39)
$cCard2  = [Drawing.Color]::FromArgb(30,39,51)
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

$form = New-Object Windows.Forms.Form
$form.Text = 'Necess'
$form.Size = New-Object Drawing.Size(840,780)
$form.MinimumSize = $form.Size
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.BackColor = $cBg
$form.Font = New-Object Drawing.Font('Segoe UI',9)
$ico = Join-Path $Root 'Necess.ico'
if (Test-Path $ico) { try { $form.Icon = New-Object Drawing.Icon($ico) } catch {} }

# cabecalho
$logo = New-Object Windows.Forms.PictureBox
$logo.Size = New-Object Drawing.Size(52,52)
$logo.Location = New-Object Drawing.Point(24,20)
$logo.SizeMode = 'Zoom'
$png = Join-Path $Root 'Necess.png'
if (Test-Path $png) { try { $logo.Image = [Drawing.Image]::FromFile($png) } catch {} }
$form.Controls.Add($logo)

$title = New-Object Windows.Forms.Label
$title.Text = 'Necess'; $title.AutoSize = $true; $title.ForeColor = $cText
$title.Font = New-Object Drawing.Font('Segoe UI',20,[Drawing.FontStyle]::Bold)
$title.Location = New-Object Drawing.Point(86,22)
$form.Controls.Add($title)

$sub = New-Object Windows.Forms.Label
$sub.Text = 'Essenciais do PC - baixe tudo num lugar so'
$sub.Font = $fDesc; $sub.ForeColor = $cMuted; $sub.AutoSize = $true
$sub.Location = New-Object Drawing.Point(88,58)
$form.Controls.Add($sub)

# painel rolavel com os itens
$list = New-Object Windows.Forms.Panel
$list.Location = New-Object Drawing.Point(24,90)
$list.Size = New-Object Drawing.Size(794,548)
$list.BackColor = $cBg
$list.AutoScroll = $true
$form.Controls.Add($list)

# helper: botao flat
function New-Btn([string]$text,[Drawing.Color]$back,[Drawing.Color]$fore) {
  $b = New-Object Windows.Forms.Button
  $b.Text = $text; $b.Font = $fBtn
  $b.FlatStyle = 'Flat'; $b.FlatAppearance.BorderSize = 0
  $b.BackColor = $back; $b.ForeColor = $fore
  $b.Cursor = 'Hand'; $b.TabStop = $false
  return $b
}

$rowW = 772; $rowH = 84; $gap = 10
$downloadRows = @()

function New-Row($item,[int]$top) {
  $card = New-Object Windows.Forms.Panel
  $card.Size = New-Object Drawing.Size($rowW,$rowH)
  $card.Location = New-Object Drawing.Point(0,$top)
  $card.BackColor = $cCard
  $list.Controls.Add($card)

  $stripe = New-Object Windows.Forms.Panel
  $stripe.Size = New-Object Drawing.Size(4,$rowH)
  $stripe.Location = New-Object Drawing.Point(0,0)
  $stripe.BackColor = $cAccent
  $card.Controls.Add($stripe)

  $name = New-Object Windows.Forms.Label
  $name.Text = $item.Name; $name.Font = $fName; $name.ForeColor = $cText
  $name.AutoSize = $true; $name.Location = New-Object Drawing.Point(20,14)
  $card.Controls.Add($name)

  $tag = New-Object Windows.Forms.Label
  $tag.Text = $item.Tag; $tag.Font = $fTag; $tag.ForeColor = $cAccent
  $tag.AutoSize = $true; $tag.Location = New-Object Drawing.Point(22,40)
  $card.Controls.Add($tag)

  $desc = New-Object Windows.Forms.Label
  $desc.Text = $item.Desc; $desc.Font = $fDesc; $desc.ForeColor = $cMuted
  $desc.AutoSize = $true; $desc.MaximumSize = New-Object Drawing.Size(500,0)
  $desc.Location = New-Object Drawing.Point(22,58)
  $card.Controls.Add($desc)

  $status = New-Object Windows.Forms.Label
  $status.Text = ''; $status.Font = $fTag; $status.ForeColor = $cMuted
  $status.TextAlign = 'MiddleRight'
  $status.Size = New-Object Drawing.Size(140,20)
  $status.Location = New-Object Drawing.Point(($rowW-310),32)
  $card.Controls.Add($status)

  # cores/refs locais, para o GetNewClosure capturar de verdade
  $C = @{ card2=$cCard2; muted=$cMuted; accent=$cAccent; err=$cErr; ok=$cOk; text=$cText }
  $frm = $form; $ddir = $DownloadDir

  if ($item.Kind -eq 'page') {
    $btn = New-Btn 'Abrir pagina' $cCard2 $cText
    $btn.Size = New-Object Drawing.Size(150,38)
    $btn.Location = New-Object Drawing.Point(($rowW-162),23)
    $card.Controls.Add($btn)
    $url = $item.Url
    $btn.Add_Click({ try { Start-Process $url } catch { $status.ForeColor=$C.err; $status.Text='nao abriu' } }.GetNewClosure())
    return
  }

  # download
  $btn = New-Btn 'Baixar' $cAccent $cAccTxt
  $btn.Size = New-Object Drawing.Size(150,38)
  $btn.Location = New-Object Drawing.Point(($rowW-162),23)
  $card.Controls.Add($btn)

  $file = $item.File; $url = $item.Url
  $state = [pscustomobject]@{ Busy=$false; Done=$false; Path=$null }

  $start = {
    if ($state.Busy) { return }
    if ($state.Done) {
      if ($state.Path -and (Test-Path $state.Path)) { Start-Process explorer.exe "/select,`"$($state.Path)`"" }
      return
    }
    $dest = Join-Path $ddir $file
    $state.Busy = $true; $btn.Enabled = $false; $btn.BackColor = $C.card2
    $status.ForeColor = $C.muted; $status.Text = 'Baixando...'
    $frm.Cursor = 'WaitCursor'
    [Windows.Forms.Application]::DoEvents()
    $ok = $false
    try {
      New-Item -ItemType Directory -Force -Path $ddir | Out-Null
      $wc = New-Object Net.WebClient
      $wc.Headers.Add('User-Agent','Mozilla/5.0')
      $wc.DownloadFile([Uri]$url, $dest)
      $wc.Dispose(); $ok = $true
    } catch {
      if (Test-Path $dest) { Remove-Item $dest -Force -ErrorAction SilentlyContinue }
      [Windows.Forms.MessageBox]::Show($_.Exception.Message, "Necess - erro ao baixar $file", 'OK', 'Error') | Out-Null
    }
    $frm.Cursor = 'Default'; $state.Busy = $false; $btn.Enabled = $true
    if ($ok) {
      $state.Done = $true; $state.Path = $dest
      $status.ForeColor = $C.ok; $status.Text = 'Concluido'
      $btn.Text = 'Abrir pasta'; $btn.BackColor = $C.card2; $btn.ForeColor = $C.text
    } else {
      $status.ForeColor = $C.err; $status.Text = 'Falhou - tentar de novo'
      $btn.BackColor = $C.accent
    }
  }.GetNewClosure()

  $btn.Add_Click($start)
  $script:downloadRows += [pscustomobject]@{ Start=$start; State=$state }
}

$top = 0
foreach ($it in $Items) { New-Row $it $top; $top += $rowH + $gap }

# rodape (fixo, abaixo do painel rolavel)
$fy = $list.Top + $list.Height + 12   # 90 + 548 + 12 = 650

$zipBtn = New-Btn 'Baixar Necess.zip' $cCard2 $cText
$zipBtn.Size = New-Object Drawing.Size(180,40)
$zipBtn.Location = New-Object Drawing.Point(24,$fy)
$form.Controls.Add($zipBtn)
$zipBtn.Add_Click({
  if (-not $zipBtn.Enabled) { return }
  $zipBtn.Enabled = $false; $t = $zipBtn.Text; $zipBtn.Text = 'Baixando...'
  $form.Cursor = 'WaitCursor'; $foot.ForeColor = $cMuted; $foot.Text = 'Baixando Necess.zip...'
  [Windows.Forms.Application]::DoEvents()
  $dest = Join-Path $DownloadDir 'Necess.zip'
  try {
    New-Item -ItemType Directory -Force -Path $DownloadDir | Out-Null
    $wc = New-Object Net.WebClient
    $wc.Headers.Add('User-Agent','Mozilla/5.0')
    $wc.DownloadFile([Uri]$ZipUrl, $dest); $wc.Dispose()
    $foot.ForeColor = $cOk; $foot.Text = 'Necess.zip salvo em .\Downloads'
  } catch {
    if (Test-Path $dest) { Remove-Item $dest -Force -ErrorAction SilentlyContinue }
    $foot.ForeColor = $cErr; $foot.Text = 'Necess.zip: falhou'
    [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Necess - erro ao baixar Necess.zip', 'OK', 'Error') | Out-Null
  }
  $form.Cursor = 'Default'; $zipBtn.Enabled = $true; $zipBtn.Text = $t
})

$allBtn = New-Btn 'Baixar tudo' $cAccent $cAccTxt
$allBtn.Size = New-Object Drawing.Size(160,40)
$allBtn.Location = New-Object Drawing.Point(488,$fy)
$form.Controls.Add($allBtn)
$allBtn.Add_Click({ foreach ($r in $script:downloadRows) { if (-not $r.State.Busy -and -not $r.State.Done) { & $r.Start } } })

$openBtn = New-Btn 'Abrir pasta' $cCard2 $cText
$openBtn.Size = New-Object Drawing.Size(150,40)
$openBtn.Location = New-Object Drawing.Point(658,$fy)
$form.Controls.Add($openBtn)
$openBtn.Add_Click({ New-Item -ItemType Directory -Force -Path $DownloadDir | Out-Null; Start-Process explorer.exe $DownloadDir })

$foot = New-Object Windows.Forms.Label
$foot.Text = 'Salva em:  .\Downloads'
$foot.Font = $fDesc; $foot.ForeColor = $cMuted; $foot.AutoSize = $true
$foot.Location = New-Object Drawing.Point(26,($fy+50))
$form.Controls.Add($foot)

[void]$form.ShowDialog()
