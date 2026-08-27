# Histórico de versões

Este documento descreve mudanças percebidas por quem usa o Sinalo. Alterações
internas de bibliotecas, testes e automação só aparecem quando tiverem impacto
direto na instalação ou no uso do aplicativo.

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
