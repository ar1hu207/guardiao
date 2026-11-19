# Prompts de IA usados no projeto

Combinado da GS: a IA foi usada como **copiloto**. Cada prompt fica registrado aqui, com o que foi feito com a
resposta. Todo integrante precisa saber explicar cada linha do que foi gerado.

## IA & Machine Learning (`ml/`)

**Ferramenta:** Claude Code (Anthropic), em 12/11/2026.

### Prompt 1: desenvolvimento da parte de ML

> "Eu vou cuidar da parte de AI e SOA desse trabalho da GS. Bora começar desenvolvendo a parte de AI. Siga o plano e
> a entrega o que pede e me entregue da forma que te pedi."

Contexto passado junto: `00-FICHA-E-CONTRATO.md`, `03-ia-machine-learning.md` e o PDF da GS.

**O que a IA fez e o que conferimos:**

| Etapa | O que a IA fez | O que a equipe conferiu |
|---|---|---|
| Dados | Achou o link oficial do Infosiga no portal do DETRAN-SP (o link antigo devolvia HTML) e baixou o ZIP e o dicionário | Que o arquivo é o oficial, as colunas exigidas e o período (2019 a set/2026) |
| Limpeza | Filtros de município, moto, período, hora, coordenada e gravidade, com a contagem de cada etapa | O funil de linhas e o motivo de começar em 2019 (antes só havia fatais) |
| Variáveis | Hora, dia, período, tipo de via e de logradouro, distância do centro. Deixou de fora o tipo de colisão | Que nenhuma variável usa informação de depois do acidente |
| Modelo | XGBoost em `Pipeline` com One-Hot, `GridSearchCV` de 18 combinações e 5 dobras só no treino | Que o teste só é usado no fim e que o `scale_pos_weight` vem do treino |
| Corte | Corte de decisão escolhido para recall ≥ 70% com previsões fora da dobra do treino | Que o corte não olha o teste |
| Saída | `zonas_risco.csv` com score por zona de ~2 km e faixa horária, usando previsões fora da amostra | Formato compatível com o contrato `GET /api/zonas-risco` |
| Textos | Relatório, README dos dados e roteiro do vídeo, com os números tirados do notebook executado | Números do relatório contra as saídas do notebook |

**Decisões que a IA tomou e a equipe precisa saber defender:**

1. **ROC-AUC como critério de escolha** do grid search, com o recall garantido depois pelo corte. Motivo: o app
   ordena zonas, e o AUC mede a qualidade dessa ordenação.
2. **Score como posição (percentil)**, não como probabilidade, porque o `scale_pos_weight` desloca as probabilidades.
3. **Tipo de colisão e veículos envolvidos fora do modelo**, mesmo melhorando a métrica, porque o app não os conhece
   antes do acidente.
4. **ROC-AUC de 0,64** reportado como sinal moderado, sem maquiagem.

## SOA (`servico-risco/`)

**Ferramenta:** Claude Code (Anthropic), em 12/11/2026.

### Prompt 2: desenvolvimento da parte de SOA

> "Começar SOA"

Contexto: `02-soa.md`, `01-csharp.md`, a rubrica do PDF e o `zonas_risco.csv` já gerado. A IA perguntou qual
caminho seguir. A equipe escolheu o **serviço de risco separado em Python**, porque o .NET não estava instalado na
máquina e a pasta `api/` (C#) fica livre para quem faz C#.

| Etapa | O que a IA fez | O que a equipe conferiu |
|---|---|---|
| Domínio | Entidades e regras puras (`FaixaHoraria`, `NivelRisco`, `ScoreFaixa`, `ZonaDeRisco`, `Coordenada`) e interfaces de repositório | Que nenhuma regra ficou no controller e que os cortes de nível são os mesmos do ML |
| Banco | SQLAlchemy com 4 tabelas relacionadas e `DATABASE_URL` por variável de ambiente | Relacionamentos, `UNIQUE`/`CHECK`/FKs e o print do banco com dados |
| Migrações | `0001_inicial` e `0002_importacoes_e_consultas` (Alembic), com `alembic check` | Que sobem, descem e sobem de novo (há teste disso) |
| API | Rotas FastAPI, DTOs, erros 400/404/409 padronizados e Swagger sem o 422 que o serviço não devolve | O formato do contrato `[{id, nome, nivel, score}]` |
| Testes | 80 testes de unidade e integração, incluindo o CSV real do ML | Rodar `python -m pytest` |
| Evidências | Prints gerados das respostas reais, do banco e do pytest (Edge headless) | Que os prints mostram saídas reais do serviço rodando |

**Decisões que a equipe precisa saber defender:**

1. **Por que um serviço separado é SOA:** cada serviço tem uma responsabilidade (risco × ocorrências), contrato REST
   estável e pode ser trocado ou escalado sozinho.
2. **Unidade de trabalho:** a importação troca 794 linhas numa transação; se uma linha falhar, nada muda.
3. **O nível é derivado do score**, nunca gravado à mão, para não divergir do modelo.
4. **Auditoria sem dado pessoal:** a consulta guarda ponto e hora, não quem consultou.

## C# (`api/`)

**Ferramenta:** Claude Code (Anthropic), em 12/11/2026.

### Prompt 3: desenvolvimento da API em C#

> "bora fazer o de C# tambem entao, 100%"

Contexto: `01-csharp.md`, `00-FICHA-E-CONTRATO.md` (rotas combinadas com o ESP32 e o app), a rubrica do PDF, o
`zonas_risco.csv` do ML e o Serviço de Risco já pronto. A IA instalou o .NET SDK 10 (com autorização) e montou a
solução em 4 projetos.

| Etapa | O que a IA fez | O que a equipe conferiu |
|---|---|---|
| Domínio | `Pessoa` abstrata → `Motoboy` e `ContatoEmergencia`; `Ocorrencia` com `Cancelar()` só se ABERTA; regras de contato, dispositivo, placa e telefone dentro das entidades | Que a regra está na entidade e não no controller |
| Interfaces + DI | `IMotoboyRepository`, `IOcorrenciaRepository`, `IZonaRiscoProvider`, `INotificador`, `IOcorrenciaService`, registradas em `Configuracao.cs` | A troca do provedor de risco (banco local ↔ Serviço de Risco) só pela configuração |
| EF Core + LINQ | `GuardiaoDbContext` (SQLite), migração `Inicial`, repositórios com `GroupBy`/`OrderBy`/`Where`, carga do CSV do ML | O print do banco com as 794 zonas e as ocorrências |
| API | 3 controllers, DTOs, ProblemDetails 400/404/409/503, Swagger com exemplos | Executar `POST /api/ocorrencias` pelo Swagger |
| Testes | 87 testes xUnit: domínio, serviços com fakes, CSV real, cliente HTTP e integração com `WebApplicationFactory` | Rodar `dotnet test` |
| Evidências | Prints do Swagger executando requisições reais (Edge headless via DevTools Protocol), do banco, dos testes e da integração SOA | Que cada print vem da API rodando |

**O que a revisão pegou no caminho:**

- Um teste mostrou que coordenada `NaN` passava pela validação; o domínio foi corrigido.
- O SMS mostrava a hora em UTC; passou a usar o fuso de São Paulo, o mesmo usado para escolher a faixa de risco.
- O exemplo do Swagger tinha uma `dataHora` no futuro, que a própria regra recusaria; o exemplo foi trocado.

**Decisões que a equipe precisa saber defender:**

1. **Grava antes de avisar:** se o aviso falhar, a ocorrência não se perde.
2. **Risco é informação extra:** se o Serviço de Risco cair, o socorro sai do mesmo jeito (há teste e print disso).
3. **Queda exige caixinha ativa, SOS não:** o SOS do app funciona mesmo com o dispositivo desligado.
4. **Caixinha só ativa com contato:** não adianta detectar a queda se não há ninguém para avisar.
5. **Dados inventados:** motoboys e contatos de demonstração são fictícios.
