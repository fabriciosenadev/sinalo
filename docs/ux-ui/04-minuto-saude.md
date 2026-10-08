# Minuto de Saúde

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Preparar vídeos disponíveis e compreender o caminho manual quando a coleção oficial não tem os dias desejados.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml, SourceFilter, DiscoverSelectedSource e fluxo por link.
Catálogo não evidencia diferença entre fonte sem atualização, resultado de pesquisa vazio e falha de rede. Vídeo por link é uma tela separada, mas seu vínculo com o destino pode ser mais claro.

## Proposta de organização e comportamento

- Título “Minuto de Saúde”; mesma estrutura e estados dos demais programas.
- Quando a descoberta retornar sem itens elegíveis, mostrar o resultado factual e as datas procuradas; não afirmar que não existe vídeo em nenhuma origem.
- Alternativa contextual “Adicionar vídeo por link” abre o workspace existente com destino Minuto de Saúde pré-selecionado; usuário pode alterar.
- Não consultar automaticamente plataformas nem mudar URL oficial. Não prometer novos vídeos ou download bem-sucedido antes de validação.
- Vídeos adicionados por link e automaticamente continuam no catálogo do programa, com data de uso e fixação claras.

## Critérios de aceitação específicos

- Estado sem conteúdo oferece próximo passo, sem culpar internet ou usuário sem diagnóstico.
- Adição por link preserva destino escolhido, passa pela fila única e só aparece pronta após validação.
- As mesmas ações de busca, reprodução e exclusão significam o mesmo nos três programas.

## Dependências e limites

Componentes de programa + Vídeo por link; acoplamento de navegação/contexto, não nova integração externa.

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
