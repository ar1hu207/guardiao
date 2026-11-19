# Guardião

## Ficha da CitizenTech

| Campo | Resposta |
|---|---|
| **Nome** | Guardião |
| **Cidade e bairro** | São Paulo — _definir o bairro/região onde a equipe entrevistou o entregador_ |
| **Cidadão beneficiado** | Entregador de moto (motoboy) que trabalha por aplicativo |
| **Problema, em uma frase** | O entregador de moto em São Paulo não sabe onde o risco é maior e, quando cai, o socorro demora porque ninguém é avisado. |
| **Como funciona** | Uma caixinha na moto percebe a queda e pede socorro sozinha; o app avisa quais regiões são perigosas naquele horário. |
| **O que cada parte faz** | ESP32 detecta a queda · API guarda e notifica · ML calcula o risco por região e horário · App mostra o risco e tem o SOS · Linux hospeda a API · Segurança protege os dados do motoboy |
| **Link do repositório** | https://github.com/ar1hu207/guardiao |
| **Indicador de impacto** | Tempo entre a queda e o aviso ao contato de emergência (meta: menos de 30 s) |
| **ODS conectados** | 3 (meta 3.6, mortes no trânsito), 9 e 11 |

## Estrutura

```
guardiao/
├── README.md        ← este arquivo
├── prompts.md       ← prompts de IA usados
├── api/             ← C#: API REST (motoboys, contatos, ocorrências, aviso aos contatos)
├── servico-risco/   ← SOA: serviço de zonas de risco (FastAPI), consumido pela API C# e pelo app
├── ml/              ← notebook, dados/README.md, requirements.txt, RELATORIO_ML.md, zonas_risco.csv
├── mobile/          ← app Expo
├── iot/             ← código do ESP32 + link do Wokwi
├── infra/           ← scripts Bash
├── security/        ← módulo de PII e Taint Analysis
└── qa/              ← PDF do Azure Boards
```

## Machine Learning

Com 124.935 sinistros reais com moto (Infosiga SP, 2019 a set/2026), um XGBoost estima a chance de um acidente
ser grave ou fatal por local e horário. No teste: ROC-AUC 0,641 e recall 0,705 da classe grave. O resultado vira
[`ml/zonas_risco.csv`](ml/zonas_risco.csv), consumido por `GET /api/zonas-risco`.
Detalhes e como rodar: [`ml/README.md`](ml/README.md) · Relatório: [`ml/RELATORIO_ML.md`](ml/RELATORIO_ML.md) · Vídeo: https://youtu.be/beAWrduNJ4c

## Serviço de Risco (SOA)

Serviço REST separado, dono das zonas de risco: FastAPI + SQLAlchemy + Alembic (2 migrações) + Swagger automático +
80 testes. A API em C# e o app consultam `GET /api/zonas-risco?lat=&lng=&hora=`; o ML publica novas versões por
`POST /api/importacoes`. Detalhes, como rodar e evidências: [`servico-risco/README.md`](servico-risco/README.md).

## API em C#

ASP.NET Core 10 + EF Core (SQLite) + Swagger + 87 testes xUnit. Recebe a queda do ESP32 ou o SOS do app, acha o
motoboy pelo `dispositivoId`, grava a ocorrência e avisa cada contato de emergência (`INotificador`: log e SMS
simulado). O risco do local vem do banco da própria API, que carrega `zonas_risco.csv`, ou do Serviço de Risco, conforme
a configuração. Como rodar, endpoints e evidências: [`api/README.md`](api/README.md).

## Integrantes

| Nome | RM |
|---|---|
| Arthur Abonizio | RM555506 |
| Gabriel Padula | RM554907 |
| Rodrigo Nakata | RM556417 |
