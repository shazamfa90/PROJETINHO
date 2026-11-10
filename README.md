# Necess

Essenciais do PC — baixe tudo num lugar só. Ferramenta portátil (Windows) com uma
GUI que baixa os instaladores que você precisa e abre as páginas oficiais dos demais.

## Como usar

1. Baixe a pasta inteira (`Necess.bat`, `Necess.ps1`, `Necess.png`, `Necess.ico`).
2. Dê **duplo clique em `Necess.bat`**.
3. Clique em **Baixar** em cada item, ou em **Baixar tudo**.
4. Os arquivos caem em uma subpasta `Downloads` ao lado do programa.

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
| Windows Update Blocker (Wub) | baixa direto (Sordum) |
| Defender Control (dControl) | baixa direto (Sordum) — zip com senha `sordum` |
| Valorant / Riot Client | **só abre a página oficial** para você baixar o launcher |

## Avisos

- **Defender Control** desliga o Windows Defender e o **Windows Update Blocker**
  bloqueia o Windows Update. Os dois reduzem a segurança do sistema. São ferramentas
  públicas e legítimas da Sordum — use com consciência, no seu próprio PC.
- O Necess apenas **baixa / abre páginas**; ele não executa nem instala nada por você.

## Teste rápido (sem abrir a janela)

```powershell
powershell -ExecutionPolicy Bypass -File Necess.ps1 -SelfCheck
```

Valida o catálogo (nomes, ações e URLs) e sai.
