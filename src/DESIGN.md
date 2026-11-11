# Direção de design do Necess

**Para quem e para quê.** Quem acabou de formatar o PC (jogador/técnico) e quer pegar os essenciais de
uma vez e saber exatamente o que aconteceu. Uma tela, uma tarefa. Escuro por razão: é ferramenta de
bancada/jogo e combina com a barra de título escura do Windows 10/11 (o app pede esse modo ao DWM).

## Tokens (`Theme.cs`)

| Papel | Valor | Contraste sobre o fundo |
|---|---|---|
| Fundo | `#141311` (grafite quente) | — |
| Texto | `#ECE8DF` | 15,2:1 |
| Texto secundário | `#A8A294` | 7,3:1 |
| Índice / desabilitado | `#7A7568` | 4,0:1 |
| Filete | `#2A2824` | decorativo |
| Borda de botão | `#716B60` | 3,5:1 (limite de componente ≥ 3:1) |
| **Acento (um só)** | `#E8A33D` | 8,6:1 — texto escuro `#1B1204` sobre ele: 8,6:1 |
| Ok / Erro | `#8DBF7B` / `#E4705F` | só para estado (8,7:1 / 6,0:1) |

O acento aparece em **três** lugares: progresso, botão principal ("Baixar tudo") e anel de foco.

## Tipografia

Segoe UI (a nativa do Windows) para a interface; Semibold só em título e nome do item. Consolas **só
para dado** (caminho, índice, fonte) e para o código do fundo. Sem caixa-alta com espaçamento, sem
título em monoespaçada.

## Espaçamento

Escala `4 / 8 / 12 / 16 / 24 / 32 / 40`. Ritmo variado de propósito: apertado dentro do item
(nome → descrição = 3 px), médio entre colunas (16), folgado entre blocos (cabeçalho → lista = 32,
lista → rodapé = 24, margens = 40/32). A altura da linha (56–68) se ajusta à tela.

## Movimento

Existe um fundo animado (pedido do usuário) e ele **tem função**: o texto que rola é o código do próprio
app e o ritmo mostra o estado — quase parado em repouso (5 px/s), acelera para 38 px/s enquanto há
download. Contenção: contraste baixo (alfa ≤ 18%), dissolve nas bordas, pausa minimizado, desacelera
em segundo plano, **não anima** se o Windows estiver com "mostrar animações" desligado, e só repinta
quando algum pixel realmente mudou.

## Estados

Toda linha diz **a causa** e o botão diz **a próxima ação**: `Servidor recusou (403)` → *Tentar de novo*;
`Sem conexão`; `Veio uma página, não o arquivo`; `Sem resposta` (45 s sem bytes). Concluído → *Abrir pasta*.

## Piso de acessibilidade

Contraste de texto ≥ 4,5:1 (conferido), botões reais (Tab, Espaço/Enter, leitor de tela com
`AccessibleName`), foco visível, escala de DPI do sistema, nenhuma informação só por cor.

## O que **não** fazemos (checklist anti-slop)

- Faixa colorida à esquerda de cada linha (a versão antiga tinha) — decoração sem informação.
- Cartões idênticos arredondados, sombras macias, glow, vidro, degradê azul→roxo.
- Ponto de status decorativo, ícones de "IA", emoji, setinhas em todo botão, `A · B · C`.
- Fake terminal / animação em loop sem propósito.
- Números inventados: o "2 de 5 baixados" é contado de verdade.
