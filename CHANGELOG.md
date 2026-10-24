# Histórico de versões

Este documento descreve mudanças percebidas por quem usa o Sinalo. Alterações
internas de bibliotecas, testes e automação só aparecem quando tiverem impacto
direto na instalação ou no uso do aplicativo.

## 1.3.1 - 24/10/2026

- Melhorada a inicialização do MPV: o Sinalo realiza até três tentativas antes
  de recorrer ao VLC, reduzindo trocas prematuras de player em computadores lentos.
- As tentativas terminam assim que o MPV inicia ou a operação é cancelada.
  Erros no próprio vídeo continuam sem abrir outro player automaticamente.

## 1.3.0 - 16/10/2026

### Categorias personalizadas

- Categorias personalizadas por nome na navegação e nos destinos de importação
  e vídeo por link, sem alterar a descoberta dos três programas existentes.
- Organização de mídias entre categorias sem mover ou duplicar arquivos,
  preservando origem, histórico e referências internas.
- Ao excluir uma categoria, escolha transferir suas mídias ou excluí-las.
  Originais externos referenciados nunca são apagados; apenas seus vínculos
  dentro do Sinalo são removidos. Falhas de exclusão ficam pendentes para nova tentativa.
- Pesquisa, filtros e botões da Biblioteca com alturas e alinhamento consistentes.
- Gerenciamento de categorias orienta ações indisponíveis, bloqueia cliques repetidos
  e permite recarregar a lista após falhas. Categoria vazia tem confirmação direta;
  categorias com mídias exigem escolher transferência ou exclusão explicitamente.
- Nomes das categorias aparecem corretamente no menu, inclusive exclusões pendentes.
  Ao abrir o gerenciamento dentro de uma categoria personalizada, ela já vem
  selecionada; na Biblioteca geral, a escolha continua sendo explícita.
- Corrigida a falha de acesso à interface por outra thread que impedia concluir
  a exclusão de categorias. As proteções durante importações e downloads continuam ativas.

## 1.2.0 - 11/10/2026

### Biblioteca de vídeos

- Nova **Biblioteca de vídeos** reúne os vídeos dos programas e MP4 locais, com
  pesquisa por nome/data, filtro de origem e ordenação.
- Biblioteca organizada em **Vídeos** e **Importar vídeos**, com lista legível,
  detalhes contextuais, seleção da tela de saída e acesso à fila de downloads.
  A edição tem Salvar/Cancelar e protege alterações não salvas.
- Importe arquivos ou pastas, incluindo subpastas. O padrão copia para o Sinalo;
  opcionalmente, mantenha o arquivo no local original, que precisa continuar acessível.
- Edite o nome exibido sem renomear arquivos; vídeos importados podem ter data
  de uso opcional. Localize arquivos ausentes e reproduza na tela configurada.
- Importações verificam espaço, permitem cancelamento e recuperam cópias
  interrompidas. A limpeza automática não apaga vídeos importados.
- Remover um importado preserva o arquivo por padrão; excluir uma cópia do
  Sinalo exige confirmação. Arquivos originais referenciados não são apagados.
- Entrega inicial restrita a vídeos MP4; áudio e imagens permanecem planejados.

### Reprodução

- Controle de vídeos na área **Reprodução**: pausar/continuar, parar, reiniciar,
  avançar para uma posição, ajustar volume e silenciar, com título, tempo e tela
  do vídeo que está efetivamente em reprodução.
- O painel acompanha o estado confirmado pelo MPV, inclusive encerramento e
  falhas. Pausar, buscar e reiniciar não contam como novas reproduções.
- Abrir a apresentação na mesma saída interrompe o vídeo pelo MPV. Quando um
  player externo estiver ativo, o Sinalo informa a necessidade de fechá-lo.

### Player e operação

- Comandos por IPC confirmados e encerramento limitado no tempo. O MPV continua
  aquecido entre vídeos; a contingência para VLC permanece para falhas de início,
  sem abrir outro player em resposta a erros do arquivo ou durante a reprodução.
- Controles e barras de posição/volume compatíveis com os temas claro e escuro.

### Melhorias

- Rolagem por pixels com transição curta na roda do mouse, evitando saltos
  por item e mantendo virtualização. Arrastar a barra e usar o teclado continua
  direto; a preferência do Windows por desativar animações é respeitada.

- Navegação com identificação da tela ativa e espaço central dedicado à tarefa.
  As ferramentas deixam de competir com uma lateral de ações de vídeos.
- Programas com pesquisa identificada, títulos legíveis e detalhes contextuais;
  reprodução explícita informa a tela de saída. Ações secundárias ficam em
  **Mais opções**; em janelas compactas, a escolha de saída pode ser expandida.
- **Downloads** acompanha pedidos e diagnósticos em qualquer tela. Progresso
  desconhecido não é apresentado como uma porcentagem de download.
- Vídeo por link organizado em consulta, qualidade e destino, com ação final
  acessível sem esticar a janela.
- Cronômetros e sorteio reorganizados; áudio ativo oferece **Parar áudio**
  mesmo ao mudar de tela. A apresentação mantém a cena aberta ao navegar.
- Configurações separadas em Aparência, Armazenamento e Programas de vídeo,
  com modos de busca explícitos e manutenção das escolhas antigas.
- Confirmação antes de transferir conteúdo, descartar alterações, reiniciar
  sorteio ou remover participantes; feedback de validação no formulário.
- Programação desta sessão em painel próprio, com posições e ações nomeadas.
  Acesso permanente pela navegação, sem precisar procurar em Mais opções.
  Continua apenas em memória, sem reprodução automática.
- Temas claro e escuro consistentes em listas, controles, rolagem e diálogos;
  Novidades destaca a versão instalada e recolhe o histórico anterior.

- A Biblioteca usa o monitor, os diálogos e o painel de downloads compartilhados.
- Vídeos importados aparecem na sessão global e podem ser reproduzidos novamente
  pelo serviço da Biblioteca, mantendo seu histórico e validando o arquivo.
- A abertura carrega a Biblioteca local em segundo plano; navegar nos programas
  lê o catálogo local sem precisar buscar vídeos na internet.

## 1.1.0 - 04/10/2026

### Novo

- A opção **Vídeo por link** na navegação permite consultar um vídeo do YouTube,
  escolher qualidade MP4, data de uso e programa de destino entre Informativo
  das Missões, Provai e Vede e Minuto de Saúde antes de colocá-lo na fila.
- Downloads por link preservam arquivos parciais após queda de conexão ou
  cancelamento; informar novamente o mesmo link e a mesma qualidade permite
  tentar retomar. Durante a reprodução, o download aguarda o player terminar.
- O vídeo só aparece como pronto offline após concluir e validar o arquivo.
  Vídeos adicionados por link são fixados por padrão para evitar limpeza
  automática.

### Ajustes

- A navegação lateral mostra uma barra de rolagem quando as opções não cabem
  na altura disponível.

## 1.0.0 - 23/09/2026

### Marco de estabilidade

- O Sinalo inicia a série 1.x como versão estável, após sua evolução e uso
  prático desde a série 0.x.
- Esta versão consolida as funcionalidades já entregues, incluindo os três
  programas de vídeo, ferramentas de apresentação e seleção manual de vídeos
  para download.
- Não há mudança no formato das configurações nem nos dados locais; a instalação
  atualiza o Sinalo preservando vídeos e preferências.

## 0.1.14 - 21/09/2026

### Novo

- Foi adicionada a ação **Procurar vídeos** em cada programa de vídeo. Ela
  permite consultar os vídeos publicados e escolher individualmente quais
  serão baixados.
- A tela de seleção informa título, data, situação e tamanho estimado de cada
  vídeo. Conteúdos já disponíveis offline ficam bloqueados para não ocupar
  espaço com um novo download.

### Ajustado

- A escolha manual é preservada ao entrar na fila: alterações posteriores na
  regra de sábados não modificam os vídeos já confirmados pelo operador.
- A documentação de configuração passa a explicar a diferença entre o fluxo
  automático **Buscar e baixar** e a escolha manual em **Procurar vídeos**.

## 0.1.13 - 19/09/2026

### Corrigido

- A atualização automática agora verifica e encerra instâncias remanescentes
  do Sinalo no mesmo computador antes de abrir o instalador. Isso evita que a
  atualização fique bloqueada por arquivos ainda em uso.
- A barra de rolagem voltou a funcionar ao ser arrastada e a janela principal
  abre de forma consistente em todos os ambientes Windows suportados.

## 0.1.12 - 19/09/2026

### Novo

- Foi adicionado o **Cronômetro de Culto**, uma ferramenta independente para
  acompanhar a programação por horário de término ou por duração.
- O cronômetro oferece ajustes rápidos de -10 a +10 minutos, opção para parar
  em zero e uma tela de apresentação compartilhada.
- Os avisos de abertura, 5 minutos e 1 minuto agora vêm incluídos no Sinalo.
  Cada um pode ser ativado ou desativado nas configurações do cronômetro.
- O painel de áudio permite escolher um aviso para teste, tocar, pausar,
  continuar, parar, avançar na faixa e ajustar o volume.

### Ajustado

- Enquanto o Cronômetro de Culto estiver em execução, suas configurações de
  horário, duração e alertas ficam protegidas contra alterações acidentais.
- Um aviso automático interrompe um áudio de teste que esteja tocando, para que
  os alertas de 5 e 1 minuto não sejam atrasados.

### Corrigido

- A janela principal passa a abrir corretamente também em ambientes Windows
  usados na validação automatizada do instalador.

## 0.1.11 - 16/09/2026

### Novo

- A limpeza automática de conteúdo pode ser ativada nas configurações. Ela
  remove mensalmente vídeos antigos de acordo com o período de retenção e a
  tolerância escolhidos pelo operador.
- Vídeos importantes podem ser **fixados** na biblioteca. Itens fixados nunca
  são removidos pela limpeza automática.

### Corrigido

- O processo de atualização agora encerra com segurança a reprodução, a tela
  de apresentação e sincronizações em andamento antes de iniciar o instalador.
- O atualizador não tenta instalar uma nova versão enquanto o processo do
  Sinalo ainda estiver aberto, evitando atualizações presas por arquivos em uso.

## 0.1.10 - 14/09/2026

### Novo

- Foi publicado o guia online de configuração do Sinalo no GitHub Pages, com
  orientações para instalar o aplicativo, configurar os programas de vídeo e
  usar a biblioteca local.
- O projeto passou a incluir os arquivos de licença e avisos de uso de
  componentes de terceiros, deixando essas informações acessíveis a quem
  instala ou distribui o Sinalo.

## 0.1.9 - 29/08/2026

### Novo

- Enquanto o Sinalo estiver aberto, ele procura uma nova versão a cada seis
  horas, sem interromper a programação. Quando encontrar uma atualização, o
  download é preparado em segundo plano para o botão **Atualizar e reiniciar**.

### Corrigido

- A reprodução no MPV passa a usar a tela realmente escolhida pelo operador,
  inclusive quando a disposição dos monitores no Windows muda. O VLC também
  recebe a numeração de tela compatível quando for usado como alternativa.

## 0.1.8 - 29/08/2026

### Novo

- Antes de iniciar uma sincronização, o Sinalo verifica se há espaço livre
  suficiente para os downloads. Durante o download, essa verificação continua
  ativa para evitar arquivos incompletos quando o disco estiver cheio.

### Corrigido

- O instalador agora inclui o runtime completo do MPV, com o executável e todas
  as bibliotecas necessárias. Assim, a reprodução rápida integrada não depende
  de o VLC estar instalado no computador.

## 0.1.7 - 27/08/2026

### Novo

- Foram adicionadas as ferramentas de apresentação **Cronômetro** e **Sorteio**.
  Elas podem ser abertas na tela escolhida para a apresentação da igreja.
- O cronômetro permite contar o tempo para cima ou para baixo, definir duração
  e escolher o formato exibido.
- O sorteio permite cadastrar nomes ou intervalos numéricos, escolher vencedores
  sem repetição, reiniciar o sorteio e limpar a lista.
- A tela principal agora mostra a versão instalada e oferece o botão
  **Novidades**, com o histórico de mudanças dividido por versão.
- As configurações permitem escolher entre tema claro, escuro ou acompanhar o
  tema do Windows.

### Ajustado

- A navegação passou a organizar os vídeos como **Programas de vídeo**, com
  ações e configurações descritas de forma mais clara.
- A lateral direita exibe apenas informações relevantes ao momento, como fila
  ativa ou detalhes do vídeo selecionado, deixando a biblioteca mais limpa.
- As barras de rolagem agora acompanham o tema do aplicativo.
- A reprodução de vídeos exige uma tela de saída selecionada, evitando que uma
  apresentação seja aberta na tela errada por engano.

## 0.1.6 - 22/08/2026

### Novo

- A tela de **Configurações** permite escolher a pasta onde os vídeos do
  Sinalo serão armazenados.
- Ao mudar essa pasta, os vídeos existentes são transferidos para o novo local
  automaticamente.
- O aplicativo agora usa o ícone oficial do Sinalo, com variação adequada para
  os temas claro e escuro do Windows.

### Ajustado

- O caminho do conteúdo local foi levado da tela principal para as
  configurações, deixando a biblioteca mais limpa.

## 0.1.5 - 22/08/2026

### Novo

- O Sinalo verifica automaticamente se há uma versão mais recente publicada.
- Quando há atualização, ela pode ser baixada dentro do aplicativo e instalada
  pelo botão **Atualizar e reiniciar**.

## 0.1.4 - 15/08/2026

### Novo

- A configuração de cada fonte permite escolher de forma independente o sábado
  anterior, o sábado atual e o próximo sábado. Se nenhum for escolhido, o
  trimestre completo é usado.
- A biblioteca agora oferece o campo **Pesquisar vídeos**, que encontra vídeos
  pelo nome ou pela data exibida, por exemplo `15/08/2026`.
- A reprodução prioriza o player rápido integrado ao Sinalo, preparado em
  segundo plano para reduzir a espera em computadores com HD. VLC e o player
  padrão do Windows continuam como alternativas de segurança.

### Corrigido

- Vídeos que já estão no computador não são baixados novamente.
- Ao excluir um vídeo, ele volta a poder ser baixado se estiver dentro da regra
  selecionada para a fonte.
- Alterar a seleção de sábados não faz os vídeos já baixados desaparecerem da
  biblioteca.
- O encerramento do aplicativo trata corretamente o fechamento do player rápido.

## 0.1.3 - 15/08/2026

### Primeira versão distribuída

- Biblioteca local para preparar vídeos antes da programação da igreja.
- Configuração das fontes **Informativo das Missões**, **Provai e Vede** e
  **Minuto de Saúde**.
- Busca de publicações oficiais e download de arquivos para uso offline.
- Escolha entre baixar o trimestre completo ou a janela semanal disponível,
  conforme a fonte.
- Reprodução de vídeos locais com preferência pelo VLC e opção de tela cheia em
  uma tela escolhida pelo operador.
- Marcação de vídeos reproduzidos e exclusão de vídeos armazenados localmente.
- Tema claro ou escuro conforme a configuração do Windows.
- Fila única de sincronização, com progresso e mensagens de status para evitar
  sobrecarga em computadores mais simples.
- Instalador para Windows 11 e publicação automatizada do instalador nas
  Releases do GitHub.

## Histórico anterior ao versionamento público

O desenvolvimento inicial ocorreu entre 02/08/2026 e 14/08/2026. Não há tags
Git para distinguir com segurança as versões 0.1.0, 0.1.1 e 0.1.2; por isso,
as funcionalidades desse período estão consolidadas na primeira versão
distribuída, 0.1.3.
