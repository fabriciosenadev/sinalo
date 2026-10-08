# Confirmações e mensagens transversais

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Entender consequências de ações e recuperar erros sem perder contexto.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MessageBox em MainWindow.xaml.cs/SettingsWindow.xaml.cs, erros de campos e estados de tarefa.
Uso misto de mensagem global, MessageBox genérico “Sinalo” e texto concatenado de exceção. Exclusão, restauração e cancelamento não têm contrato visual comum.

## Proposta de organização e comportamento

- Separar erro de campo (inline), resultado de tarefa (na área da tarefa), operação global (indicador) e confirmação irreversível (diálogo).
- Diálogos com verbo/objeto concretos, ação segura como padrão e cancelar/Esc. Exemplo: “Excluir vídeo do computador?” com título do vídeo, consequência e opção de baixar novamente.
- Exclusão física nunca apresentada como remover só da lista; restauração de URL altera formulário, não salva automaticamente.
- Sucesso discreto textual, erro com ação e diagnóstico; não transmitir significado só pela cor.
- Componentes próprios de diálogo podem substituir MessageBox quando necessário ao tema/acessibilidade; manter pickers nativos de arquivo/pasta. Não redesenhar instalador ou player externo nesta branch.
- Publicar glossário e usar rótulos iguais no app/site; mensagens técnicas só em detalhes sanitizados.

## Critérios de aceitação específicos

- Operador distingue cancelar fila, remover programação, excluir arquivo e fechar apresentação.
- Foco retorna à ação original; Enter não confirma destruição inadvertidamente.
- Textos longos quebram sem esconder opções; tema/teclado compatíveis.

## Dependências e limites

Todos os planos; mudança visual não relaxa confirmação nem proteção de arquivos.

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
