# Informativo das Missões

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Encontrar o vídeo pela data ou pelo título e reproduzir um arquivo pronto, distinguindo busca automática de seleção manual.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: catálogo, pesquisa e ações; HomeViewModel.ApplyFilters.
Cards de 205 DIP com títulos truncados, data e status no mesmo DockPanel dificultam leitura. O título genérico “Vídeos” não situa o programa. Glifo de reprodução não substitui botão explícito. O estado vazio depende da mensagem global.

## Proposta de organização e comportamento

- Título “Informativo das Missões”, resumo de vídeos prontos e janela de datas contextual.
- Busca “Pesquisar por título ou data” com exemplo visível curto e limpar pesquisa.
- Lista compacta legível: título como informação principal, data de uso, disponibilidade e fixação separados; título completo nos detalhes. Não depender de miniaturas reais.
- “Buscar e baixar” como ação principal de preparação e “Escolher vídeos para baixar” como alternativa. Explicar que a regra seleciona buscas, não garante publicação.
- Selecionar mostra detalhes; “Reproduzir na Tela …” é ação explícita. Duplo clique pode permanecer como atalho, não única forma.
- Estados diferentes: sem vídeos locais, nenhum resultado da pesquisa, descoberta sem arquivo publicado, download em fila, erro com diagnóstico. Vídeos online não entram como prontos.

## Critérios de aceitação específicos

- Títulos como “O sonho de Enoc — Parte 1” continuam distinguíveis sem abrir vários cards.
- Pesquisa por nome/data e seleção sobrevivem à atualização do catálogo quando o item existir.
- O operador distingue download concluído, nenhum novo vídeo e falha.
- Fixar/excluir preservam as regras e mostram efeito concreto.

## Dependências e limites

Depende da estrutura e componentes comuns; não depende da Biblioteca geral. Layout comum aos três programas, estados próprios.

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
