# Necess

Essenciais do PC — baixe tudo num lugar só. Ferramenta portátil (Windows) com uma
GUI que baixa os instaladores que você precisa e abre as páginas oficiais dos demais.

## Como usar

1. Baixe o **`Necess.zip`** e **extraia tudo** (botão direito no ZIP → *Extrair tudo…*).
   Não abra o `.bat` de dentro do ZIP — ele precisa estar ao lado do `Necess.ps1`.
2. Dê **duplo clique em `Necess.bat`**.
3. Clique em **Baixar** em cada item, ou em **Baixar tudo**.
4. Os arquivos caem em uma subpasta `Downloads` ao lado do programa.
5. Ao terminar, o Necess **mostra uma notificação do Windows e abre a pasta** com o
   arquivo selecionado (no *Baixar tudo* a pasta abre uma vez só, no fim).

O botão **Baixar Necess.zip** (rodapé) baixa a versão empacotada do próprio
Necess, direto do repositório — útil para pegar/repassar a última versão.

Portátil: não instala nada, não precisa de dependências (usa o PowerShell e o .NET
que já vêm no Windows 10/11).

## O que ele pega

| Item | Ação |
|------|------|
| Visual C++ Runtimes All-in-One (Jul 2025) | abre a página (TechPowerUp) |
| Visual C++ Redistributable x64 | baixa direto (Microsoft) |
| DirectX End-User Runtime (dxwebsetup) | baixa direto (Microsoft) |
| WinRAR (pt-BR, 64 bits) | baixa direto (RARLAB, sempre a versão estável mais nova) |
| Windows Update Blocker (Wub) | baixa direto (Sordum) |
| Defender Control (dControl) | baixa direto (Sordum) — vem em **.rar com senha `sordum`**: abra com o WinRAR |
| Valorant / Riot Client | **só abre a página oficial** para você baixar o launcher |

Todo arquivo baixado é conferido pelos primeiros bytes (`MZ` = .exe, `PK` = .zip,
`Rar!` = .rar). Se o servidor devolver uma página de erro em vez do arquivo, o Necess
descarta e mostra *Falhou* — nunca salva uma página HTML com nome de `.exe`/`.zip`.

## Avisos

- **Defender Control** desliga o Windows Defender e o **Windows Update Blocker**
  bloqueia o Windows Update. Os dois reduzem a segurança do sistema. São ferramentas
  públicas e legítimas da Sordum — use com consciência, no seu próprio PC.
- O Necess apenas **baixa / abre páginas**; ele não executa nem instala nada por você.
- O WinRAR é shareware (período de avaliação) — baixado do site oficial da RARLAB.

## Se algo não funcionar

- **Nada acontece ao abrir o `.bat`**: você abriu de dentro do ZIP. Extraia tudo antes.
- **Uma linha mostra *Falhou***: clique de novo em *Baixar*. Detalhes técnicos ficam em
  `Necess.log` (na pasta do programa).
- O Windows pode mostrar o aviso do SmartScreen na primeira execução do `.bat`
  (arquivo baixado da internet): *Mais informações → Executar assim mesmo*.

## Teste rápido (sem abrir a janela)

```powershell
powershell -ExecutionPolicy Bypass -File Necess.ps1 -SelfCheck
```

Valida o catálogo (nomes, ações e URLs) e sai. Com `-Online` ele também acessa a
internet e confere se **cada link ainda entrega o tipo de arquivo certo**:

```powershell
powershell -ExecutionPolicy Bypass -File Necess.ps1 -SelfCheck -Online
```
