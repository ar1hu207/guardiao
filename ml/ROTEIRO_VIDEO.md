# Roteiro do vídeo — IA & ML (até 5 minutos)

Gravar a tela com o notebook aberto e rolar até cada seção. Subir no YouTube como **não listado**.
Os tempos somam 4 min 50 s.

## 1. Problema e pergunta (0:00 – 0:30) · *tela: título do notebook*

> "O Guardião é uma solução para o motoboy de aplicativo em São Paulo. Só de janeiro a setembro deste ano, 2.038
> motociclistas morreram no trânsito do estado. A nossa pergunta de Machine Learning é: dado um acidente com moto
> na cidade, com este local, dia e horário, qual a chance de ele ser grave ou fatal? É uma classificação binária, e o
> resultado vira o score de risco que o app mostra."

## 2. Dados (0:30 – 1:30) · *tela: seções 2 e 3 (tabela da fonte e funil de limpeza)*

> "Os dados são reais e públicos: o Infosiga, do DETRAN-SP, baixado do portal oficial em 12 de novembro. Usamos de
> 2019 a setembro de 2026, porque antes de 2019 o Infosiga só registrava acidentes fatais.
> Partimos de 1,45 milhão de sinistros no estado, filtramos a capital, só os que envolvem moto, tiramos linhas sem
> hora, coordenadas erradas e sem gravidade, e chegamos a 124.935 sinistros. Só 11% são graves: a classe é minoria.
> Criamos variáveis de hora, dia, fim de semana, período, tipo de via e distância do centro, e deixamos de fora o
> tipo de colisão, porque o app precisa avisar antes do acidente."
>
> *Mostrar o gráfico por hora:* "Já dá para ver: de madrugada a taxa de graves passa de 25%."

## 3. Modelo, hiperparâmetros e validação cruzada (1:30 – 3:00) · *tela: seções 6, 7, 8*

> "Escolhemos o XGBoost: o dado é tabular, mistura números e categorias, ele trata classe desbalanceada com o
> scale_pos_weight e mostra a importância de cada variável.
> Dividimos 80% para treino e 20% para teste, estratificado. Testamos 18 combinações de três hiperparâmetros:
> profundidade da árvore, taxa de aprendizado e número de árvores, com validação cruzada de 5 dobras só no treino,
> o que dá 90 treinos.
> *Mostrar a tabela e o gráfico da grade:* As linhas tracejadas são o treino e as cheias a validação. Nos modelos
> mais complexos o treino chega a 0,98 e a validação cai para 0,57: o modelo decorou. A validação cruzada escolheu
> o mais simples: profundidade 3, taxa 0,1, 100 árvores.
> Como deixar de avisar um trecho perigoso é pior do que avisar à toa, ajustamos o corte para garantir 70% de recall,
> usando só o treino."

## 4. Resultados de treino e teste (3:00 – 4:30) · *tela: seção 9 e 10*

> "No teste: ROC-AUC de 0,64 e recall de 70%. O modelo encontra 7 de cada 10 acidentes graves. O treino deu 0,66:
> a diferença é de só 0,02, então não há overfitting.
> *Matriz de confusão:* acertamos 1.964 dos 2.785 graves. O custo é alarme a mais, que para um aviso de atenção é aceitável.
> *Importância:* o que mais pesa é a hora, a distância do centro e se a via é rodovia.
> É um sinal moderado, e somos honestos sobre isso: onde e quando explicam parte da gravidade; velocidade e capacete
> explicam o resto.
> *Mapa das zonas:* com o modelo geramos o zonas_risco.csv, 222 regiões de 2 km em quatro faixas horárias, que a API
> do Guardião usa para responder o app."

## 5. O que isso significa para o motoboy (4:30 – 4:50) · *tela: seção 12*

> "Para o motoboy: na madrugada de fim de semana, um acidente de moto tem quase 3 vezes mais chance de ser grave do
> que à tarde num dia útil. Na região da SP-270, de madrugada, é 3 vezes a média da cidade. O modelo não prevê que
> o acidente vai acontecer. Ele mostra onde e quando o tombo costuma ser pior, e é isso que o Guardião avisa."
