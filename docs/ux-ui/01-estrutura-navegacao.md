# Estrutura principal e navegação

Data: 08/10/2026. Criticidade/carga: Alta · grande.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

O operador precisa localizar sua tarefa, saber qual tela está ativa e continuar acompanhando operações de fundo.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml, HomeViewModel.cs e MainWindow.xaml.cs.
Três colunas fixas de 250/*/290 e duas faixas superiores consomem a área útil. A mesma lateral de ações dos programas aparece nas ferramentas. A navegação tem hover/foco, mas não um indicador persistente de página selecionada. OperationMessage agrega tarefas distintas; atualizar um download pode substituir o contexto de outra tarefa.

## Proposta de organização e comportamento

- Cabeçalho compacto com marca, versão/Novidades, atualização e Configurações; marca preservada, sem redesenhar logo.
- Navegação com seleção persistente: Programas de vídeo (três programas), Adicionar vídeo (Vídeo por link), Ferramentas de apresentação (Cronômetro, Cronômetro de Culto, Sorteio). Não criar Biblioteca geral nesta branch.
- Workspace central com título e descrição curta. Faixa de sábados somente nos programas.
- Tela de saída compartilhada na barra do workspace quando aplicável. Usar o serviço/persistência existente e exibir “Tela 1 · Principal”, nunca objetos serializados.
- Ações específicas junto à tarefa, sem lateral global acumulando download, áudio, sorteio e exclusão. Painel de detalhes contextual quando necessário.
- Indicador global compacto de downloads em andamento abre o painel da fila em qualquer workspace; aviso de áudio/apresentação ativos continua visível ao trocar de página.
- Navegar não cancela operação ativa. No modo compacto, textos/botões quebram linha e detalhes substituem temporariamente o conteúdo; não comprimir três colunas.

## Critérios de aceitação específicos

- Cada entrada tem um estado selecionado inequívoco, diferente de foco/hover.
- As ferramentas não exibem ações dos programas nem datas de sábado.
- A fila e a tela de saída permanecem acessíveis; mudar workspace não muda monitor.
- Na janela mínima todas as ações essenciais estão acessíveis, sem rolagem horizontal.

## Dependências e limites

Base para os demais planos. Pode compartilhar diretrizes com Biblioteca e player, mas não importar suas branches.

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
