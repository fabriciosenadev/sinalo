# Cronômetro de Culto e áudio

Data: 08/10/2026. Criticidade/carga: Alta · grande.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Operar contagem e avisos sonoros sem perder acesso ao áudio que está tocando.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: IsWorshipTimerWorkspace; WorshipTimerViewModel.cs e WorshipTimerAudioPlayer.cs.
Um cartão longo mistura tempo, seis ajustes adjacentes, campos dos dois modos, alertas e player. Player fica abaixo da dobra; é possível não perceber que um áudio segue ativo. Ajustes não indicam unidade.

## Proposta de organização e comportamento

- Área operacional fixa: tempo, término previsto, estado, ligar/desligar, ajustes com unidade “min”.
- Configuração “Hora de término/Duração” mostra apenas campo aplicável. Separar alertas de configuração básica.
- Player sempre acessível quando áudio ativo: nome do aviso, origem automática/manual, posição, duração, tocar/pausar/continuar/parar, volume. Ao sair da página, indicador global “Áudio do culto em reprodução” com parar e voltar.
- Em telas largas, contagem e áudio em áreas distintas; compactas, controle de áudio ativo prioritário e detalhes/configuração com rolagem. Não esconder Parar áudio em expander.
- Textos distintos “Desligar cronômetro”, “Parar áudio” e “Fechar apresentação”, conforme efeitos reais dos serviços. Documentar e testar o efeito de desligar sobre áudio; não alterar regra apenas para caber no layout.
- Caminho/origem dos arquivos e erro de áudio sob detalhe, sem expor texto técnico no estado principal.

## Critérios de aceitação específicos

- Operador encontra Parar áudio em até uma ação mesmo fora desta página.
- Avisos de 5/1 min e abertura continuam automáticos e configuráveis como hoje.
- Áudio indisponível informa erro sem travar contagem.
- Ajustar tempo não dispara alertas duplicados; toda regressão funcional é testada.

## Dependências e limites

Saída e sinalização global de tarefas. Preservar MP3 e controles completos já existentes; não depender do controle de MPV de vídeo.

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
