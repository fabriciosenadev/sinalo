# Programação simples em memória

Data: 08/10/2026. Criticidade/carga: Média · pequena/média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Reconhecer uma ordem preparada de vídeos sem confundi-la com downloads ou reprodução automática.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: ScheduleItems; HomeViewModel.AddSelectedToSchedule/Move/Remove.
Itens da programação aparecem dentro do painel de vídeo selecionado, sem cabeçalho próprio; controles ↑/↓/× não explicam ação. A lista pode desaparecer junto com seleção.

## Proposta de organização e comportamento

- Seção própria “Programação desta sessão”, fora dos detalhes do item, inicialmente somente no workspace de vídeo.
- Apresentar posição, título, programa e estado; ações “Mover para cima”, “Mover para baixo”, “Remover da programação” com nomes acessíveis.
- Expor adicionar somente se o comando estiver realmente disponível; auditar handler já existente e ligar explicitamente, sem presumir botão funcional pelo método.
- Aviso curto “Mantida apenas enquanto o Sinalo está aberto”; não prometer persistência, duplicatas ou reprodução automática.
- Seção recolhível com resumo para não competir com catálogo; empty state explica como adicionar.

## Critérios de aceitação específicos

- Remover da programação não exclui arquivo.
- Ordem corresponde à coleção atual; mover extremos sem efeito proibido tem estado.
- Seção não some ao mudar seleção do vídeo, e não é chamada fila de downloads.

## Dependências e limites

Catálogo e shell; não implementar roteiro/coletâneas/fila automática de outras specs.

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
