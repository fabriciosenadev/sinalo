# Análise adicional: menu e categorias existentes

Data: 16/10/2026. Branch: feature/custom-media-categories.
Solicitação: compreender os dois problemas do print, sem aplicar correção nesta rodada.

## Evidência real consultada

O banco local do operador foi aberto por SQLite em `mode=ro` e `query_only=ON`.
Foi criada uma cópia consistente via SQLite Backup em ambiente separado, sem
apagar/recriar registros ou executar migração no banco original.

| Categoria | Sistema | Revisão | Exclusão pendente | Associações explícitas | Entradas no journal |
| --- | --- | --- | --- | --- | --- |
| Teste | Não | 1 | Não | 0 | 0 |
| Teste 2 | Não | 0 | Não | 0 | 0 |

Ambas foram reconhecidas pelo serviço real de leitura/inspeção como vazias.
Não há coluna de idade do cadastro nem regra que impeça exclusão por uma categoria
ter sido criada antes da correção. A revisão 1 de Teste é válida; só bloqueia se
o objeto selecionado estiver com revisão diferente da persistida.

## 1. Defeito confirmado no menu

`MainWindow.xaml` fornece um TextBlock como Content do botão de categoria.
`Button.Navigation`, em `VisualIdentity.xaml`, já define um ContentTemplate que
cria outro TextBlock com `Text={Binding}`. O conteúdo passa a ser um controle,
convertido para texto, em vez do nome da categoria.

Isso explica `System.Windows.Controls.TextBlock` na navegação, não uma perda de
registros. Os dados estavam carregados tanto no menu quanto no gerenciamento.
O título Teste ao fundo também é compatível com o filtro de categoria já aplicado.

Correção indicada: passar texto como conteúdo do botão, reutilizando o template
de navegação, preservando o objeto/ID no Tag. Nome ou indicação de pendência não
devem ser transformados em caminho nem ID.

## 2. Seleção da Biblioteca não é seleção do gerenciamento

`Category_Click` modifica filtro, título e destino de importação da Biblioteca.
`ManageCategories_Click` cria um novo CategoryManagerWindow, sem passar a categoria
que estava aberta. O VM do gerenciador começa com Selected nulo e, ao carregar,
preserva esse nulo. Assim, abrir Teste no menu e depois gerenciar não pré-seleciona
Teste no diálogo. Excluir fica desabilitado até selecionar uma linha nele.

É uma descontinuidade de contexto que torna o fluxo confuso. A captura comprova
ausência de seleção naquele momento, não comprova que o operador não tenha tentado
selecionar antes. Não atribuir a falha relatada ao operador sem evidência.

## 3. Verificações executadas com os registros existentes

O diagnóstico separado `Sinalo-category-diagnostics-20261016` utiliza a cópia do
banco e o gerenciador WPF real. Um wrapper recusa todas as mutações do serviço.
A rotina sem UI também usa os ViewModels reais. Nenhuma exclusão foi executada,
nem mesmo na cópia.

| Verificação | Resultado |
| --- | --- |
| Carregar Teste e Teste 2 já persistidas | Ambas apareceram na lista. |
| Seleção programática + preparação, em cada registro | Selected correto, CanDelete=true, confirmação preparada e habilitada. |
| Clique real no texto Teste | Linha destacada, campo preenchido e Excluir habilitado. |
| Clique real em Excluir para Teste | Abriu confirmação da categoria correta, identificada como vazia. |
| Cancelar por Esc | Nenhuma alteração; seleção preservada. |
| Clique na área vazia da linha de Teste 2 | Selecionou a linha inteira e habilitou Excluir. |
| Clique real em Excluir para Teste 2 | Abriu confirmação habilitada da categoria correta. |
| Cancelar por Esc | Nenhuma alteração; seleção preservada. |

Esses cliques foram feitos pela skill computer-use, não apenas por atribuição
direta a Selected em um teste. O diagnóstico não tinha importação/download ou
reprodução ativos. O primeiro harness da MainWindow não expôs uma janela ao
controle do Windows; por isso os cliques desta rodada foram feitos no gerenciador
isolado, e não certificados como reprodução completa do bootstrap do App.

## Conclusão e limites

- Renderização do menu: causa confirmada no código, compatível com o print.
- Persistência/retrocompatibilidade dos dois registros: não há bloqueio identificado;
  carregamento, seleção e preparação de exclusão funcionaram usando os dados copiados.
- Contexto ao abrir o gerenciador: a seleção da Biblioteca não é transportada.
- Exclusão após confirmar: não executada nesta análise. Não afirmar resolução ou
  compatibilidade ponta a ponta apenas porque a confirmação abriu.
- Falha de clique ou bloqueio específico no aplicativo aberto pelo usuário:
  ainda não reproduzido. Registrar se o clique destaca a linha, preenche o nome,
  habilita Excluir, abre a confirmação e qual mensagem aparece após a tentativa.

## Ajustes recomendados para uma próxima implementação

1. Corrigir o conteúdo textual dos botões de categoria no menu.
2. Ao abrir o gerenciador a partir de uma categoria, preservar seu ID e selecionar
   o registro atual após carregar; na Biblioteca geral, manter escolha explícita.
3. Mostrar claramente a categoria selecionada e manter orientação de seleção.
4. Cobrir texto efetivamente renderizado do menu e cliques reais com registros
   pré-existentes; verificar estado antes/depois de recarga, não apenas contagem.
5. Se o bloqueio persistir após seleção, capturar a etapa/mensagem do App completo
   e reproduzir antes de modificar regras de persistência ou exclusão.

Não foram alterados arquivos de produção, banco original, categorias ou mídias.
Foi acrescentado este registro e criado apenas o harness diagnóstico externo.
Nenhum commit, push ou release foi realizado.

## Implementação posterior à análise

Após autorização, o menu passou a fornecer o objeto da categoria diretamente
ao template textual do botão, eliminando a conversão indevida de um TextBlock.
O gerenciador recebe o ID da categoria personalizada atualmente aberta, busca
seu registro atualizado e a seleciona após carregar. A Biblioteca geral e outros
workspaces não herdam filtros antigos como seleção do gerenciamento.

Uma falha no carregamento conserva o ID inicial para tentar novamente. Após
carregar, recargas respeitam a seleção e o rascunho do operador; um ID removido
não seleciona outra categoria automaticamente. Não há alteração de esquema,
migração destrutiva ou remoção das categorias do usuário.

A confirmação final de exclusão dos dois registros reais continua não executada.
Os testes automatizados usam serviços e bancos isolados; a validação do usuário
no aplicativo completo permanece necessária antes de declarar o relato encerrado.

## Causa identificada após novo print do usuário

A mensagem de acesso a objeto de outra thread identificou um defeito na composição
real em App.xaml.cs: o callback `busy` do serviço consultava `mainWindow.Library`.
Esse getter lê uma DependencyProperty WPF, mas RemoveAsync executa o callback
em Task.Run. A exceção ocorria na primeira verificação, antes de iniciar exclusão
ou journal. As categorias antigas não eram a causa dessa falha.

O novo `CategoryOperationState` remove a dependência da janela: observa mudanças
de importação e publica um indicador com Volatile; a fila fornece seu snapshot
sincronizado. O serviço mantém as duas verificações de ocupação, antes e depois
do gate, sem despachar chamadas síncronas para a interface.

A matriz integrada usa agora esse mesmo componente de estado do bootstrap.
Casos adicionais verificam importação, sincronização, nova tentativa, ocupação
enquanto aguarda o gate e botão real com confirmação/cancelamento e SQLite.
Todos operam em dados isolados; o banco e as categorias reais não foram alterados.
