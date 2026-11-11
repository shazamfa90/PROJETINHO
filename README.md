# Necess

Essenciais do PC num lugar só. **Um único `Necess.exe`** (com ícone) que baixa os instaladores
que você precisa e abre as páginas oficiais dos demais.

## Como usar

1. Baixe o **`Necess.zip`**, extraia e dê **duplo clique em `Necess.exe`**. Não instala nada e não
   precisa de nada além do Windows 7/10/11 (usa o .NET Framework 4 que já vem nele).
2. Clique em **Baixar** em cada item, ou em **Baixar tudo**.
3. Os arquivos caem na pasta `Downloads` ao lado do `.exe`.
4. Ao terminar, o Necess **mostra uma notificação do Windows e abre a pasta** com o arquivo
   selecionado (no *Baixar tudo* a pasta abre uma vez só, no fim).

> Se você abrir o `.exe` de dentro do ZIP, os arquivos vão para `Downloads\Necess` na sua pasta de
> usuário (a pasta temporária onde o Explorer extrai seria apagada).

O botão **Baixar Necess.zip** (rodapé) baixa a versão empacotada do próprio Necess, direto do repositório.

## O que ele pega

| Item | Ação |
|------|------|
| Visual C++ Runtimes All-in-One | abre a página (TechPowerUp) |
| Visual C++ Redistributable x64 | baixa direto (Microsoft) |
| DirectX End-User Runtime (dxwebsetup) | baixa direto (Microsoft) |
| WinRAR (pt-BR, 64 bits) | baixa direto (RARLAB, sempre a versão estável mais nova) |
| Windows Update Blocker (Wub) | baixa direto (Sordum) |
| Defender Control (dControl) | baixa direto (Sordum) — vem em **.rar com senha `sordum`**: abra com o WinRAR |
| Valorant / Riot Client | **só abre a página oficial** para você baixar o launcher |

Todo arquivo é conferido pelos primeiros bytes (`MZ` = .exe, `PK` = .zip, `Rar!` = .rar). Se o servidor
devolver uma página de erro em vez do arquivo, o Necess descarta e diz a causa na linha (*Sem conexão*,
*Servidor recusou (403)*, *Veio uma página, não o arquivo*…) — nunca salva HTML com nome de `.exe`/`.zip`.
Quando o Google Drive pede confirmação ("não consegue verificar vírus"), o Necess segue o *Download anyway*.

## Avisos

- **Defender Control** desliga o Windows Defender e o **Windows Update Blocker** bloqueia o Windows
  Update. Os dois reduzem a segurança do sistema. São ferramentas públicas da Sordum — use com
  consciência, no seu próprio PC.
- O Necess apenas **baixa / abre páginas**; ele não executa nem instala nada por você.
- O WinRAR é shareware (período de avaliação), baixado do site oficial da RARLAB.
- O `.exe` não tem assinatura digital paga: na primeira vez o SmartScreen pode avisar —
  *Mais informações → Executar assim mesmo*. O código-fonte está em `src/` e pode ser auditado.
- Erros técnicos vão para `Necess.log` (ao lado do `.exe`).

## Compilar

O código está em `src/` (C#, WinForms), os insumos de ícone em `assets/` (embutidos no `.exe`). Em
Linux/macOS/Windows com o compilador do Mono (`mono-mcs`):

```bash
./build.sh        # gera Necess.exe e Necess.zip
```

Testes do núcleo (sem janela): `mcs -target:exe -out:CoreTests.exe tests/CoreTests.cs src/Core.cs && mono CoreTests.exe --online`.
A direção visual e o raciocínio do design estão em [`src/DESIGN.md`](src/DESIGN.md).
