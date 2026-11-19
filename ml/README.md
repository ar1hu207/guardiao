# ml/ — Score de risco por região e horário

> **Integrantes:** Arthur Abonizio (RM555506) · Gabriel Padula (RM554907) · Rodrigo Nakata (RM556417)

Parte de **Inteligência Artificial & Machine Learning** do Guardião.

**Pergunta:** dado um sinistro com moto em São Paulo, com este local, dia e horário, qual a chance de ele ser grave ou fatal?
**Dados:** Infosiga SP (DETRAN-SP), 124.935 sinistros reais com moto na capital, de 2019 a set/2026.
**Modelo:** XGBoost, com 18 combinações de hiperparâmetros e validação cruzada de 5 dobras só no treino.
**Resultado no teste:** ROC-AUC 0,641 · recall da classe grave 0,705 · F1 0,239 · sem overfitting (treino 0,658).

## Arquivos

| Arquivo | O que é |
|---|---|
| [`guardiao_risco_ml.ipynb`](guardiao_risco_ml.ipynb) | Notebook **executado**, com todas as saídas e gráficos |
| [`RELATORIO_ML.md`](RELATORIO_ML.md) | Relatório do problema à conclusão |
| [`dados/README.md`](dados/README.md) | Fonte, link, período e dicionário das colunas |
| `dados/processados/sinistros_moto_sp_2019_2026.csv` | Base limpa usada no modelo |
| [`zonas_risco.csv`](zonas_risco.csv) | **Saída para a API**: score e nível por zona e faixa horária |
| `figuras/` | Gráficos (PNG), tabela do grid search e resumo das métricas (JSON) |
| [`ROTEIRO_VIDEO.md`](ROTEIRO_VIDEO.md) | Roteiro e falas do vídeo de até 5 minutos |
| [`video/guardiao_ia_ml.mp4`](video/guardiao_ia_ml.mp4) | Vídeo de 4 min 45 s (narração gerada a partir do roteiro, com os gráficos do notebook) |
| `requirements.txt` | Bibliotecas com versão fixa |

## Como rodar

Requisito: Python 3.12.

```bash
cd ml
python -m venv .venv
.venv\Scripts\activate        # Windows  (Linux/macOS: source .venv/bin/activate)
pip install -r requirements.txt
jupyter nbconvert --to notebook --execute --inplace guardiao_risco_ml.ipynb
```

Ou abra com `jupyter notebook` e rode célula por célula. Na primeira execução o notebook baixa o ZIP oficial do
Infosiga (141 MB) para `dados/raw/`. A execução completa leva cerca de 6 minutos (90 treinos do grid search).

## Contrato com a API (`zonas_risco.csv`)

| Coluna | Exemplo | Uso |
|---|---|---|
| `zona_id` | 78 | id da zona (célula de ~2 km) |
| `nome` | SP-270 e entorno | logradouro com mais sinistros na célula |
| `latitude`, `longitude` | -23.59, -46.79 | centro da célula; a API busca a zona mais próxima de `lat`/`lng` |
| `faixa_horaria` | MADRUGADA | MADRUGADA, MANHA, TARDE ou NOITE |
| `hora_inicio`, `hora_fim` | 0, 5 | a API casa o parâmetro `hora` com essa faixa |
| `score` | 1.0 | 0 a 1, posição da zona no ranking de risco (vai direto para o JSON) |
| `nivel` | ALTO | BAIXO (< 0,40), MEDIO (0,40 a 0,79) ou ALTO (≥ 0,80) |
| `risco_relativo` | 1.66 | probabilidade do modelo ÷ média da cidade |
| `prob_modelo` | 0.7844 | probabilidade média do modelo (fora da amostra) |
| `taxa_grave_observada` | 0.3421 | % real de sinistros graves na zona e faixa |
| `n_sinistros` | 38 | sinistros usados (mínimo 30) |

Resposta esperada da API, conforme o contrato do projeto:

```json
[{ "id": 78, "nome": "SP-270 e entorno", "nivel": "ALTO", "score": 1.0 }]
```
