# Diagnóstico de categorias e controles da Biblioteca

Data: 16/10/2026. Branch: `feature/custom-media-categories`.
Escopo: análise e testes; nenhuma correção de comportamento foi aplicada nesta rodada.

**Atualização posterior:** este arquivo preserva o diagnóstico anterior às correções.
O ciclo implementado e os novos resultados/limites estão em
[validação da estabilização](validacao-estabilizacao-categorias-2026-10-16.md).
A falha original permanece não reproduzida; não considerar este diagnóstico prova
de resolução apenas pelo resultado dos testes do candidato.

## Ambiente e limites

- Controles WPF e serviços reais da branch, compilados em Release.
- Harness separado em `E:\_projetos\_iasd\Sinalo-qa-categories`, com banco isolado
  em `bin\Release\net10.0-windows\isolated-data`.
- Cliques reais pela automação do Windows, não apenas chamadas de comandos do VM.
- Gerenciador real e Biblioteca real carregados com a identidade visual do aplicativo.
  O harness não reproduz toda a inicialização do App, fila ou downloads.
- Nenhuma categoria, configuração ou mídia do usuário foi modificada.
- Após autorização específica do usuário, a exclusão definitiva da categoria vazia
  criada no teste foi executada pela interface. Essa categoria havia sido renomeada
  de `QA criada manualmente` para `QA renomeada` durante o teste de renomeação.
  Nenhuma mídia estava associada; a remoção foi definitiva, sem arquivo físico afetado.
- Os testes de rede, instalação, HD/projetor e DPI diferentes não foram executados.

## Resultados observados manualmente

| Caminho | Resultado |
| --- | --- |
| Criar sem nome | Recusado, com mensagem de validação. |
| Renomear sem seleção | Recusado, com mensagem para selecionar categoria. |
| Subir sem seleção | **Defeito:** informa “Alteração concluída”, sem executar alteração. |
| Excluir sem seleção | **Defeito:** não abre diálogo nem explica a ausência de seleção. |
| Criar categoria válida | Categoria criada e selecionada. |
| Criar nome duplicado | Recusado, com mensagem de duplicidade. |
| Renomear para nome de outra categoria | Recusado, preservando nome persistido; campo volta ao nome anterior. |
| Renomear para nome válido | Nome atualizado na lista. |
| Subir categoria selecionada | Ordem alterada na lista. |
| Abrir exclusão de categoria recém-criada | Diálogo abriu normalmente. |
| Transferir sem destino | Continuar desabilitado. Não há explicação contextual do motivo. |
| Selecionar destino válido | Continuar habilitado. A própria categoria não é oferecida como destino. |
| Selecionar exclusão definitiva | Continuar habilitado; abre uma segunda confirmação. |
| Cancelar na confirmação final | Categoria preservada. |
| Cancelar na escolha de transferência | Categoria preservada. |
| Fechar gerenciador e retornar à Biblioteca | Funcionou. |
| Confirmar exclusão definitiva da categoria vazia autorizada | Categoria removida, campo limpo e mensagem “Alteração concluída”. |
| Fechar e reabrir o gerenciador após excluir | Categoria continuou ausente; demais categorias de teste preservadas. |

O relato de impossibilidade de excluir **ainda não teve a causa exata reproduzida**:
o fluxo completo de exclusão definitiva de uma categoria vazia funcionou no
ambiente isolado, inclusive após fechar e reabrir o gerenciador.
O fluxo assume transferência como padrão, exige destino mesmo em categoria vazia
e não explica o botão desabilitado. Isso é um problema de usabilidade confirmado,
mas não prova que seja a única causa do relato do usuário.

## Medidas e causa do desalinhamento

Medidas obtidas da árvore visual WPF após renderizar a Biblioteca:

- Botões Pesquisar/Atualizar lista/Adicionar vídeo por link: altura 40 DIPs.
- Campo de pesquisa, Origem e Ordenar por: altura aproximada 36,62 DIPs.
- Filtro Categoria: altura 40 DIPs.
- Topo dos botões: Y aproximado 263,99; topo dos campos: Y aproximado 276,80.
  Coordenadas relativas ao host de teste; a diferença é de cerca de 12,81 DIPs.

O tema compartilhado define mínimo de 40 para campos e combos e altura de 40
para botões. A Biblioteca sobrescreve o mínimo para 36 em alguns campos.
Os campos ficam em StackPanels com rótulos, mas os botões são centralizados
na altura inteira da linha do WrapPanel. Categoria usa um rótulo diferente
dos demais. Assim, corrigir apenas a altura não resolve o alinhamento vertical.

## Outros riscos identificados por leitura do código

Estes itens não foram reproduzidos manualmente como falhas de infraestrutura:

- `Loaded` do gerenciador chama `RefreshAsync` em evento async void sem captura
  local. Uma falha de leitura do banco pode escapar do tratamento da tela.
- Os botões de categoria não têm condições de habilitação vinculadas a seleção,
  limites de ordenação ou Busy. Há proteção parcial no VM, mas a interface aceita
  ações que não podem produzir resultado e não explica todos os bloqueios.
- Durante a inspeção anterior à confirmação de exclusão, Busy ainda não é ativado;
  existe uma janela para novas interações. É necessário testar reentrância no App
  completo, não assumir que os testes do serviço cobrem esse comportamento.
- Mensagens de exceção são exibidas diretamente em alguns pontos, sem tradução
  operacional para erro de acesso, arquivo bloqueado ou indisponibilidade do banco.
- Mover cadastros existentes na importação pode ficar visível sem haver cadastros
  existentes; o handler retorna sem feedback nessa condição.

## Testes automatizados complementares

Executado nesta rodada:

`dotnet test Sinalo.slnx --configuration Release --no-restore --filter "FullyQualifiedName~Category"`

Resultado: **28 aprovados, 0 falhas, 0 ignorados**.
Incluem proteção de arquivos referenciados, transferência, arquivo bloqueado,
retomada de exclusão pendente e confirmação desatualizada. São testes de serviço,
VM e WPF automatizado. A exclusão pelo botão real foi validada separadamente apenas
para a categoria vazia autorizada; não comprova o cenário do App completo com mídias.

## Encaminhamento recomendado

1. Exclusão manual de categoria vazia concluída no banco isolado, com persistência
   e feedback verificados. Falta reproduzir o estado específico relatado no App
   completo sem operar sobre mídias reais, incluindo os callbacks da janela principal.
2. Padronizar controles em 40 DIPs e alinhar as ações à linha dos campos, separando
   rótulos da área de ações e mantendo a adaptação para janelas menores.
3. Desabilitar ações sem seleção/sem efeito e mostrar o motivo dos bloqueios.
4. Tornar explícitas as opções Transferir/Excluir, com orientação sobre destino;
   categoria vazia não deve impor uma escolha de destino sem necessidade.
5. Tratar carga inicial, falhas operacionais e reentrância no gerenciador.
6. Acrescentar testes dos botões e diálogos, incluindo cancelamento nas duas etapas,
   seleção/alternância de opções e falhas injetadas. Revalidar no App completo.

Há aceite técnico do cenário isolado de exclusão de categoria vazia, não do cenário
relatado no App completo. Não há recomendação de publicar esta branch ainda.
