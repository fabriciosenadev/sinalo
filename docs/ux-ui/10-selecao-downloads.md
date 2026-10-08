# Escolher vídeos para baixar

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Escolher arquivos elegíveis sabendo quantos e quanto será baixado, sem selecionar itens já locais.

## Diagnóstico individual — evidência de código

Arquivos/áreas: ManualVideoSelectionWindow.cs.
Rows montadas em StackPanel sem virtualização, checkbox sem nome acessível associado. Rodapé com contagem/tamanho e botões em DockPanel pode colidir. Lista só tem fluxo vertical, sem filtro.

## Proposta de organização e comportamento

- Título e programa, instrução curta. Tabela/lista: seleção, título, data, estado e tamanho com label acessível por linha.
- Lista virtualizada, busca por título/data se volume justificar. “Selecionar disponíveis” e “Limpar seleção”, respeitando itens inelegíveis.
- Estados locais e sem arquivo permanecem visíveis e não marcáveis; explicar motivo, sem vermelho para estado normal.
- Resumo fixo em duas linhas no compacto: quantidade, soma conhecida e aviso de tamanhos desconhecidos; “Adicionar selecionados à fila” descreve a ação real.
- Cancelar/fechar não altera catálogo offline ou fila; lembrar preseleção das regras existentes.

## Critérios de aceitação específicos

- Não marcar/baixar offline novamente; seleção padrão é preservada.
- Tamanho desconhecido não aparece como 0 MB definitivo.
- Busca não perde seleção fora dos resultados; confirmação envia exatamente IDs elegíveis selecionados.

## Dependências e limites

Fila e componentes de lista/modal; descoberta não muta offline antes da confirmação.

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
