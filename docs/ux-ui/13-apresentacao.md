# Tela de apresentação e escolha de saída

Data: 08/10/2026. Criticidade/carga: Alta · média/grande.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Exibir tempo/resultado legível no projetor, sem informações do operador distraindo a audiência.

## Diagnóstico individual — evidência de código

Arquivos/áreas: PresentationWindow.xaml, PresentationOutputService.cs e handlers abrir/fechar.
Padding fixo80, texto72 e tema global limitam adaptação à resolução. Instrução Esc sempre na audiência. Mesma cena tem resultado curto ou nome muito longo; apresentação topmost na tela principal exige escape confiável.

## Proposta de organização e comportamento

- Separar superfície do operador e saída do público. Cronômetros: tempo dominante; sorteio: vencedor dominante com quebra/adaptação; cena de conferência mostra identificação da tela.
- Fundo de apresentação de alto contraste consistente com marca, inicialmente escuro fixo independente do tema do operador; não criar nova configuração obrigatória. Validar em projetor real.
- Margens relativas/área segura e escala por tamanho útil. Não reintroduzir barra de progresso no projetor.
- Mostrar instrução Esc na cena de conferência, não permanentemente sobre tempo/vencedor; manter Escape funcional em todas as cenas e fechamento visível no operador.
- “Tela de saída” comum para vídeo/apresentação; indicar uso atual e bloqueio/motivo ao tentar uso conflitante. Monitor desconectado tem mensagem e recuperação explícita.
- Abrir apresentação da ferramenta envia cena dessa ferramenta; botão de conferir saída envia cena de conferência distinta.

## Critérios de aceitação específicos

- Esc sempre fecha mesmo na tela principal; controle do operador também fecha.
- Abrir/fechar apresentação não inicia/para silenciosamente timer ou áudio.
- Nome longo e timer cabem em 1024×768/1920×1080; texto distante legível validado fisicamente.
- Troca/desconexão de monitor preserva resolução do serviço e não abre em destino incorreto.

## Dependências e limites

Serviço de saída existente, cenas e estrutura global; não trazer duas saídas ou controles MPV novos.

## Implementação e verificação

1. Criar o layout e os estados de interface utilizando os componentes de [identidade visual](00-identidade-visual.md).
2. Ligar aos comandos/serviços existentes, sem duplicar persistência ou alterar regras silenciosamente.
3. Cobrir estados vazio, preenchido, ocupado, indisponível e erro aplicáveis; conferir teclado, foco, leitor de tela e ambos os temas.
4. Renderizar em 1280×720 e 1920×1080, testar janela mínima e DPI real 100%, 125% e 150%. Não considerar renderização simulada prova de DPI real.
5. Executar testes unitários, integração e ponta a ponta relevantes e a cobertura total mínima de 75% de linhas e branches.
6. Validar manualmente com o operador e computador com HD. Ao concluir, revisar necessidade de atualizar guia do site, arquitetura, roadmap e changelog; não publicar feature ainda pendente.

Status: implementado em `feat/experiencia-usuario`; aceite manual pendente.
Ver [execução e evidências](execucao.md). Os critérios físicos de HD, projetor,
DPI real e leitor de tela continuam no roteiro de validação do operador.
