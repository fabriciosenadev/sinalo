# Adicionar vídeo por link

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Concluir o fluxo consultar → escolher qualidade/destino/data → adicionar à fila sem adivinhar o próximo passo.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: LinkedVideoWorkspace; InspectLinkedVideo_Click e QueueLinkedVideo_Click.
Todos os campos estão no mesmo cartão longo, com 28 DIP de padding e vários intervalos de 20 DIP. Informações ainda inexistentes ocupam espaço; ação no final da rolagem; status mistura etapas.

## Proposta de organização e comportamento

- Dividir visualmente em 1. Consultar vídeo, 2. Preparar download e 3. Confirmar destino. Etapas no mesmo workspace, não assistente modal obrigatório.
- URL e “Consultar” quebram para duas linhas no espaço compacto; erro junto à URL. Reconsultar não enfileira.
- Após sucesso mostrar título/publicação e qualidade em texto humano. Destino e data de uso explicitamente distintos da data de publicação.
- Rodapé de ação estável com “Adicionar à fila”; resumo de qualidade, programa e data antes da confirmação. Não iniciar download ao selecionar formato.
- Estados consultar/consultando/encontrado/sem formatos/falha/enfileirado/duplicado. Mostrar progresso de download na fila, sem transformar a consulta em player.
- Link “Ver downloads” após enfileirar. Manter dados úteis do formulário para corrigir falhas.

## Critérios de aceitação específicos

- Botão final acessível na janela mínima sem redimensionar.
- Consultar não duplica download; clique repetido durante operação tem bloqueio e razão visível.
- Alterar a URL invalida formatos antigos até nova consulta.
- Destino/data/qualidade confirmados correspondem ao pedido enviado.

## Dependências e limites

Fila única e componentes de formulário. Preservar serviços de descoberta/download existentes; mudança visual não amplia fontes.

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
