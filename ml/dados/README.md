# Dados — Infosiga SP

Todos os dados deste projeto são **reais e públicos**. Nada foi simulado.

## Fonte

| Item | Valor |
|---|---|
| **Base** | Infosiga SP — Sistema de Informações Gerenciais de Sinistros de Trânsito |
| **Responsável** | DETRAN-SP / Governo do Estado de São Paulo |
| **Portal** | https://infosiga.detran.sp.gov.br/ → *Dados Abertos e Documentos* → *Download dos dados* |
| **Link do arquivo** | https://infosiga.detran.sp.gov.br/painel/download/file/dados_infosiga.zip |
| **Dicionário oficial** | https://infosiga.detran.sp.gov.br/painel/download/file/Infosiga_dicionario_dados.zip (versão 1.8, de 15/10/2026) |
| **Data do download** | 12/11/2026 |
| **Versão do portal** | 3.1, dados atualizados em 19/10/2026 |
| **Período disponível no arquivo** | dezembro/2014 a setembro/2026 |
| **Período usado no modelo** | **01/01/2019 a 30/09/2026** |
| **Licença** | dados abertos do Governo do Estado de São Paulo |

**Por que 2019 em diante:** até 2018 o Infosiga registrava só sinistros fatais. Os não fatais entram a partir de
2019. Usar os anos anteriores inflaria artificialmente a classe "grave".

## O que tem no ZIP

`dados_infosiga.zip` (141 MB) traz três tabelas ligadas pela chave `id_sinistro`, cada uma dividida em três arquivos
(`2015-2021`, `2022-2024`, `2025-2026`):

| Arquivo | Uma linha é… | Usado? |
|---|---|---|
| `sinistros_*.csv` | um sinistro (local, data, hora, veículos, gravidade) | **Sim** |
| `pessoas_*.csv` | uma pessoa envolvida (idade, sexo, lesão) | Não (dado pessoal, e a gravidade já vem agregada em `sinistros`) |
| `veiculos_*.csv` | um veículo envolvido | Não |

Formato: CSV separado por `;`, codificação Latin-1, vírgula como separador decimal, datas `dd/mm/aaaa`.

## Onde ficam os arquivos

| Pasta | Conteúdo | No Git? |
|---|---|---|
| `dados/raw/` | ZIP oficial e dicionário em PDF | **Não** (141 MB passa do limite do GitHub). O notebook baixa sozinho do link oficial na primeira execução. |
| `dados/processados/sinistros_moto_sp_2019_2026.csv` | Base já limpa usada no modelo (124.935 linhas) | Sim |

## Recorte e limpeza

| Etapa | Linhas |
|---|---:|
| Sinistros no estado de SP (2014–2026) | 1.450.961 |
| Município de São Paulo | 343.241 |
| Com pelo menos uma moto (`qtd_motocicleta ≥ 1`) | 135.811 |
| A partir de 01/01/2019 | 134.338 |
| Sem `id_sinistro` duplicado | 134.338 |
| Com hora válida | 134.243 |
| Com coordenada dentro da cidade (lat −24,01 a −23,35; lng −46,83 a −46,36) | 129.069 |
| Com gravidade informada (ao menos uma vítima fatal, grave ou leve) | **124.935** |

## Dicionário das colunas usadas

### Colunas originais do Infosiga

| Coluna | Tipo | Descrição (dicionário oficial) | Uso no projeto |
|---|---|---|---|
| `id_sinistro` | inteiro | Identificador único do sinistro | chave / remoção de duplicados |
| `data_sinistro` | data `dd/mm/aaaa` | Data do sinistro | dia da semana, mês, fim de semana, filtro de período |
| `hora_sinistro` | texto `HH:MM` | Hora do sinistro | variável `hora` e `periodo` |
| `municipio` | texto | Município (caixa alta, sem acento) | filtro `SAO PAULO` |
| `logradouro` | texto livre | Logradouro do sinistro | `tipo_logradouro` e nome da zona de risco |
| `tipo_via` | texto | `VIAS URBANAS` ou `ESTRADAS E RODOVIAS` | variável do modelo |
| `administracao` | texto | Órgão que administra a via (prefeitura, concessionária, DER…) | variável do modelo |
| `latitude`, `longitude` | decimal (WGS84) | Coordenadas do sinistro | variáveis do modelo e grade das zonas |
| `qtd_motocicleta` | inteiro | Quantidade de motos envolvidas | filtro (≥ 1) |
| `qtd_gravidade_fatal` | inteiro | Pessoas mortas em até 30 dias | alvo |
| `qtd_gravidade_grave` | inteiro | Pessoas com lesão grave | alvo |
| `qtd_gravidade_leve` | inteiro | Pessoas com lesão leve | alvo |
| `tipo_registro` | texto | `SINISTRO FATAL`, `SINISTRO NAO FATAL` ou `NOTIFICACAO` | conferência (não entra no modelo) |

### Colunas criadas

| Coluna | Como foi criada |
|---|---|
| `grave_fatal` (**alvo**) | `1` se `qtd_gravidade_fatal > 0` ou `qtd_gravidade_grave > 0`; `0` se o pior ferimento foi leve |
| `hora` | hora inteira (0–23) de `hora_sinistro` |
| `dia_semana` | 0 = segunda … 6 = domingo |
| `fim_de_semana` | 1 se sábado ou domingo |
| `mes` | 1–12 |
| `periodo` | `MADRUGADA` (0–5h), `MANHA` (6–11h), `TARDE` (12–17h), `NOITE` (18–23h) — mesmo corte do Infosiga |
| `tipo_logradouro` | primeira palavra do logradouro padronizada: `AVENIDA`, `RUA`, `RODOVIA` (SP-xxx, BR-xxx, acessos), `ESTRADA`, `MARGINAL`, `PONTE_VIADUTO_TUNEL`, `OUTROS` |
| `dist_centro_km` | distância em linha reta (haversine) até a Praça da Sé |

## Colunas deixadas de fora de propósito

- **Tipo de colisão (`tp_sinistro_*`) e veículos envolvidos (`qtd_automovel`, `qtd_caminhao`…)**: só se sabe depois do
  acidente. O app avisa o motoboy *antes*, sabendo só onde e quando ele está.
- **Dados das pessoas (idade, sexo, profissão)**: dado pessoal, não é necessário para o score por região.

## Dado opcional não usado

Chuva por hora do INMET (estação Mirante de Santana) ficou como próximo passo: enriqueceria o modelo, mas exigiria
cruzar data/hora com outra base. Está registrado nas limitações do `RELATORIO_ML.md`.
