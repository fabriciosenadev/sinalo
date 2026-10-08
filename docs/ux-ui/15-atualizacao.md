# Atualização do aplicativo

Data: 08/10/2026. Criticidade/carga: Alta · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Perceber atualização pronta e escolher momento seguro de instalar sem confundir com download de vídeo.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: IsUpdateAvailable; CheckForUpdateAsync e InstallUpdate_Click.
Cartão de update aumenta cabeçalho e reduz workspace. Estado de preparar encerramento existe, mas a transição pode parecer travamento; botão precisa razão quando desabilitado.

## Proposta de organização e comportamento

- Indicador global discreto “Nova versão …” e painel com versão atual/nova, download do instalador e ação “Atualizar e reiniciar”.
- Separar Downloads de vídeos de Atualização do Sinalo por título e ícone/texto. Download de update permanece automático conforme serviço atual.
- Estados encontrado/baixando/pronto/preparando encerramento/falha. Indeterminado quando não há progresso conhecido.
- Antes de instalar com tarefa ativa, comunicar que serão encerrados vídeo, áudio, apresentação e sincronizações; solicitar confirmação contextual.
- Durante shutdown bloqueiar disparos repetidos e mostrar etapa enquanto UI ainda estiver viva; não prometer progresso do instalador externo nem inventar cancelamento seguro durante encerramento.
- Falha de rede não ocupa alerta permanente interrompendo culto; possibilidade de nova checagem usa mecanismo existente ou subatividade explicitamente aprovada.

## Critérios de aceitação específicos

- Nenhuma instalação dispara só ao notificar.
- Botão pronto mantém tamanho do shell; falha explica próximo passo sem prender a interface.
- Fluxo antigo de updater/installer e verificação de processo continuam testados; não alterar protocolo neste redesenho.

## Dependências e limites

Shell e encerramento seguro existente; ajustes de protocolo fora do escopo visual.

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
