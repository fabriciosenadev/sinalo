# Histórico de versões

Este documento descreve mudanças percebidas por quem usa o Sinalo. Alterações
internas de bibliotecas, testes e automação só aparecem quando tiverem impacto
direto na instalação ou no uso do aplicativo.

## Não publicado

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
