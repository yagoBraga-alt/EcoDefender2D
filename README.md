# EcoDefender 🌱♻️

Jogo 2D educacional sobre **reciclagem**, desenvolvido em **Unity 6** (template 2D Platformer Microgame).

> **Disciplina:** Desenvolvimento de Jogos e Realidade Virtual — IFPI Campus Parnaíba
> **Professor:** Denylson Melo
> **Trabalho 03 — Protótipo de Jogo 2D**

## 👥 Integrantes do grupo

- *(preencher nome 1)*
- *(preencher nome 2)*
- *(preencher nome 3)*
- *(preencher nome 4)*

## 🎮 Sobre o jogo

Lixo cai do céu sobre a cidade. O jogador **defende** o chão coletando cada item e
**entregando na lixeira de reciclagem correta** antes que ele grude no chão.
Antes de cada onda, o jogador posiciona **coletores** em pontos estratégicos do mapa.

### Objetivo educacional
Ensinar a separação correta do lixo segundo o padrão brasileiro de coleta seletiva:
- 🟢 **Orgânico** (verde) — restos de comida, cascas, folhas
- 🔴 **Plástico** (vermelho) — garrafas, sacolas, embalagens
- 🔵 **Papel** (azul) — jornais, caixas, cadernos
- 🟡 **Metal** (amarelo) — latas, tampas, alumínio

## 🕹️ Como jogar

| Ação | Controle |
|------|----------|
| Mover | `A` / `D` ou setas |
| Pegar lixo | encostar nele |
| Entregar | `E` ou `Espaço` perto da lixeira certa |
| Posicionar coletor | clicar no mapa (fase de preparação) |
| Reiniciar (fim de jogo) | botão na tela ou tecla `R` |

## 🎲 Elementos do design (sorteados)

| Elemento | Valor | Como aparece no jogo |
|----------|-------|----------------------|
| Gênero | Action | ritmo rápido, lixo caindo |
| Tema | Sticky | lixo gruda no chão se não coletado |
| Interação | Defend | defender o chão da poluição |
| Forma | Vertical Scroller | lixo cai de cima |
| Mecânica 1 | Pick up and deliver | coletar e entregar na lixeira |
| Mecânica 2 | Simultaneous action | vários lixos ao mesmo tempo |
| Mecânica 3 | Secret unit deployment | posicionar coletores antes da onda |

## 🛠️ Tecnologia

- **Unity 6** (6000.x)
- Toda a cena é construída por código (`GameBootstrap.cs`) — não há montagem manual no Inspector
- Arte e áudio **gerados proceduralmente** em runtime (sem assets externos)
- Estilo visual: **neo-brutalismo**

## ▶️ Como rodar no editor

1. Abrir o projeto no Unity 6
2. Menu **EcoDefender → Create Game Scene** (cria `Assets/Scenes/GameScene.unity`)
3. Abrir a cena e apertar **Play**
