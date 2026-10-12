# Provai e Vede

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Preparar o trimestre ou sábados configurados sem baixar duplicados e reproduzir o vídeo correto.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml, configuração do programa, seleção automática e manual.
Mesmo catálogo estreito e ações longas dos demais programas. Regra trimestral/semanal fica distante da ação; o usuário pode interpretar ausência de download como defeito.

## Proposta de organização e comportamento

- Reutilizar a estrutura dos programas, com título “Provai e Vede” e resumo “Busca configurada: …”.
- Resumo distingue trimestre completo e combinação de sábados. Link “Alterar regra” abre Configurações no programa, preservando pesquisa/seleção.
- Datas da janela e trimestre são contexto secundário, não categorias concorrentes.
- Detalhes mostram título, data, pronto para reproduzir e fixação. A fila informa quantos novos arquivos foram preparados e quantos já estavam locais, quando os dados forem disponíveis.
- Usar os mesmos rótulos e ações de Missões, sem botão com nome inteiro do programa.

## Critérios de aceitação específicos

- Trimestre inteiro e sábados selecionados não sugerem regras simultâneas.
- “Nenhum vídeo novo” não significa apagar ou ocultar arquivos já baixados.
- Lista distingue nomes longos e dias diferentes; reprodução explícita usa o monitor salvo.

## Dependências e limites

Mesmo componente de catálogo de Missões; manter política e seleção existentes, sem depender de outra feature.

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
