# Fila de downloads

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Acompanhar pedidos, distinguir aguardando, baixando, validando e concluído, e recuperar falhas.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: SynchronizationQueueItems; MainWindow.xaml.cs: fila/cancelamento.
Fila vive na lateral global, competindo com outras tarefas. Estado e nome em DockPanel estreito; details acumulam. Cancelar fila pode ser interpretado como cancelar só um arquivo.

## Proposta de organização e comportamento

- Acesso global “Downloads” com quantidade ativa; painel dedicado com resumo e lista rolável.
- Cada pedido: programa/vídeo, estado textual, detalhe/progresso quando conhecido, diagnóstico quando existe. Não exibir porcentagem inventada para validação/descoberta.
- Estados esperando/buscando/baixando/validando/concluído/sem novos vídeos/falhou/cancelado.
- “Cancelar todos os downloads” explica que cancela ativo e pendentes; não inventar cancelamento por item suportado apenas no desenho.
- Erro num programa não oculta resultados anteriores nem impede mostrar próximo pedido.
- Painel ligado ao estado atual da Home, inclusive após reconfigurar; trocar workspace/fechar painel não cancela fila.

## Critérios de aceitação específicos

- Acesso disponível em cronômetros, sorteio e link.
- Concluído não declara vídeos offline quando nenhum foi preparado.
- Cancelamento/erro nunca tornam .part reproduzível; progresso permanece responsivo em HD.

## Dependências e limites

Estrutura principal e diagnóstico. Não é fila de reprodução; sem dependência da branch MPV.

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
