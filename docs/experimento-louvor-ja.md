# Louvor JA Flute — integração local

Origem: branch `experiment/louvor-ja-mock` (nome original do experimento). A implementação
real está em Debug e Release, **desativada por padrão nas Configurações**.
Escopo exclusivo: Louvor JA Flute (`louvorja/flute-app`), não Louvor JA legado.
Entrega publicada na **1.6.0**, após aceite manual em 02/11/2026. O simulador anterior
continua disponível apenas como dependência explícita dos testes automatizados.

## Como testar com a instalação real

1. Instalar Sinalo 1.6.0 ou superior; para desenvolvimento, executar Sinalo.App
   pelo Visual Studio ou `dotnet run --project src/Sinalo.App` na raiz do projeto.
2. Em **Configurações > Integrações**, marcar **Habilitar integração com Louvor JA
   Flute** e salvar. Abrir **INTEGRAÇÕES > Louvor JA Flute** na navegação.
3. Conferir o executável (padrão: `%LocalAppData%\Programs\Louvor JA\Louvor JA.exe`)
   e clicar **Abrir Louvor JA**, caso o aplicativo ainda não esteja aberto.
   Para outra pasta, usar **Escolher executável…** e **Salvar caminho e porta**.
   O perfil atual é usado por padrão, nunca um nome de usuário fixo.
4. No Louvor JA, abrir **Configurações > Transmissão (OBS / vMix)**, selecionar
   **Localhost (Apenas este computador)**, conferir a porta (padrão 7071) e ativar
   o servidor. Ativação e alterações são feitas pelo operador, não pelo Sinalo.
5. Informar a mesma porta no painel Sinalo. **Abrir Louvor JA** agora aguarda
   até 30 segundos e conecta automaticamente quando o servidor estiver pronto.
   Se ativou o servidor depois do prazo, usar **Tentar conectar novamente**.
   Ao iniciar o Sinalo com o Louvor JA já aberto no caminho configurado, essa
   verificação também ocorre, sem exigir entrar na tela da integração.
6. Pesquisar uma música por nome, número ou letra; selecionar o item e o modo
   cantado, instrumental ou sem áudio; clicar **Abrir música no Louvor JA**.
7. Controlar play/pausa, slides, volume ou encerramento pelo Sinalo. A música e
   suas imagens/áudios precisam estar disponíveis no próprio Louvor JA.

**Os comandos são reais.** Antes de abrir música, conferir volume e telas de
saída no Louvor JA. O monitor escolhido no Sinalo não configura o Louvor JA.
Instrumental só é habilitado para músicas que declaram esse recurso.

Desconectar e fechar o Sinalo não encerram áudio nem fecham o Louvor JA:
para isso, usar **Encerrar música** enquanto estiver conectado. Cancelar uma
espera não desfaz comando já recebido. Diante de falha/timeout, conferir o
Louvor JA antes de repetir, principalmente comandos de alternância play/pausa.

## Segurança, ciclo de vida e comportamento

- Somente HTTP para o endereço numérico 127.0.0.1, porta 1..65535. Sem DNS,
  proxy, redirecionamentos, conexões remotas ou alteração do arquivo do Louvor JA.
- Ao iniciar o Sinalo, carrega caminho/porta e verifica em segundo plano o processo
  instalado. Se já estiver aberto, aguarda prontidão e conecta. Se estiver fechado,
  não abre automaticamente. **Abrir Louvor JA** reutiliza a instância existente ou
  abre uma única instância e aguarda prontidão. Sem download de executável.
- Prontidão exige caminho do processo compatível, ping com identidade LouvorJA e
  status válido. Espera máxima de 30 segundos, consultas espaçadas em 2 segundos,
  canceláveis. Caminho inacessível/permissões insuficientes são estado desconhecido,
  nunca justificativa para abrir uma possível segunda instância.
- Servidor e inicialização automática do servidor são configurados no Louvor JA.
  Na versão analisada, requisições de loopback dispensam token.
- Verificação de identidade via ping (app=LouvorJA/running). Catálogo e estado
  são lidos por HTTP, sem acesso direto a banco, mídia ou preferências externas.
- Comandos são serializados e não são repetidos automaticamente. Após envio,
  até sete snapshots são consultados com intervalos de 300 ms para observar
  a mudança; ok HTTP sozinho não é confirmação. Prazo por requisição: 3 segundos.
- Snapshot não publica ID da música: a observação de abertura compara título e
  estado. Não prova identidade única entre músicas homônimas, nem som/telão físicos.
- Sessão independente da navegação: verifica processo a cada 2 segundos. Quando
  conectado, consulta ping; o status completo só é atualizado com painel visível
  ou por consulta explícita. Fechar Louvor JA limpa apresentação e desabilita
  controles, mesmo fora da tela. Nova instância detectada permite nova tentativa
  limitada de conexão. Sem SSE e sem repetir comandos de reprodução.
- Servidor indisponível após o prazo ou comunicação perdida com processo aberto
  exigem **Tentar conectar novamente** (ou reabertura do processo). Não há ciclo
  infinito de tentativas HTTP quando indisponível. **Desconectar/Cancelar espera**
  suspendem reconexão automática até ação explícita nesta sessão, mesmo se o
  Louvor JA fechar e reabrir. Isso não para música nem fecha o programa.
- Requisições são serializadas por sessão, sem sobrepor monitoramento e comandos.
  Alterar caminho/porta invalida respostas anteriores; a nova configuração exige
  ação explícita. Fechar Sinalo cancela esperas e monitoramento, drena operações e
  libera transporte, sem encerrar o Louvor JA ou sua reprodução.
- O painel recusa iniciar/retomar música se detectar vídeo ativo, apresentação
  aberta ou áudio do cronômetro tocando no Sinalo. Não para conteúdo silenciosamente.
  Não é um árbitro global entre os aplicativos: reproduções iniciadas diretamente
  no Louvor JA ou posteriormente no Sinalo ainda exigem coordenação do operador.
- Diagnóstico limitado a 100 entradas em memória, com trechos de respostas
  (inclusive títulos/letras). Nada é gravado no log em disco.
- Busca musical dinâmica: debounce de 350 ms, Enter/botão para consulta imediata,
  campo editável durante a busca e indicador local de espera/consulta. Cada texto
  cancela a solicitação anterior e invalida seus resultados, inclusive se o
  transporte responder depois do cancelamento. Campo vazio limpa resultados e
  seleção sem HTTP. Desconexão, troca de configuração e descarte cancelam a busca.
  Consultas compartilham o gate da operação e a sessão, sem abrir músicas ou
  bloquear a digitação; falhas antigas canceladas não derrubam a sessão atual.
- Caminho e porta são salvos explicitamente em
  `%LocalAppData%\Sinalo\data\louvor-ja-integration.json`, com arquivo temporário
  e substituição após escrita. Sem configuração, usa o caminho do perfil e porta
  7071. Falha de leitura é visível, sem sobrescrever dados silenciosamente.
  Nenhuma migração de SQLite ou alteração nas regras de downloads/player.
  Desde o incremento de 02/11/2026, cliente, transporte, ViewModel e View estão
  presentes também no Release; a preferência controla a criação e execução.
  `Enabled` ausente em JSON legado significa false e preserva caminho/porta.
  Store compartilhado serializa atualizações de conexão e habilitação sem
  sobrescrever o outro campo. Habilitar/desabilitar não exige executável existente.
  Desativar cancela e descarta o runtime, sem fechar Flute ou parar sua música.

## Alcance

Baseado em Louvor JA 2.0.2 / commit 060ebe1. Rotas: ping, status, search-songs,
open-song (tag 1/2/3), play-pause, song-slides (next/previous), volume e close-media.
Quando o Louvor JA publica conteúdo bíblico, o painel só consulta sua apresentação;
não envia comandos de Bíblia. Liturgia, cronômetros, sorteio, fila e configuração
de projetores não são controlados. Nenhum código-fonte do Louvor JA foi copiado.

## Testes e documentação

- Simulador em memória: cenários normal, indisponível, lento, vazio, JSON inválido,
  aceito sem efeito e desconexão. Ele não é a conexão usada pela tela do operador.
- Transporte real: teste com servidor TCP isolado em porta aleatória de loopback,
  sem qualquer comando para a instalação Louvor JA do usuário.
- Restrições de destino, porta, serialização JSON/URL, confirmação, bloqueio por
  reprodução local, abertura inválida, observação de mudanças e perda de conexão.
- WPF em temas claro/escuro; montar a View isolada não aciona rede/processo.
  Na composição real do aplicativo, a sessão habilitada é iniciada no Loaded da janela
  principal, independentemente de navegar ao painel. Os testes injetam processos
  e clientes isolados, incluindo prontidão lenta, timeout, fechamento, reabertura,
  cancelamento, serialização e desconexão manual; não abrem a instalação real.
  Capturas em TestResults/louvor-ja. Conferir também isolamento no build Release.
- Validação física de música, som e monitores reais permanece com o operador.
- Guia do operador em `docs/guia-operador-louvor-ja.md` e `site/louvor-ja.html`,
  com instruções da 1.6.0, habilitação opcional e distinção do Flute/legado.

Validação anterior ao novo ciclo automático, em 01/11/2026: 539 testes Debug
aprovados; cobertura total 89,11% de linhas e 78,43% de branches. Teste Release
de exclusão das implementações passou. Capturas foram revisadas. Consulta somente leitura a
127.0.0.1:7071/api/ping encontrou conexão recusada; servidor não foi ativado pelo
agente. Música, áudio, projeção e abertura do executável real não foram acionados
durante a validação automatizada. O operador confirmou posteriormente que o
fluxo integrado básico funcionou. O ensaio na igreja e o teste manual das novas
opções de caminho/salvamento permanecem pendentes.

### Validação do ciclo automático — 01/11/2026

- Suíte Debug completa: **556 testes aprovados**, nenhum ignorado/falho.
- Cobertura total: **88,90% de linhas e 78,10% de branches**, acima da meta de 75%.
- Build Release e teste de exclusão das implementações experimentais aprovados.
- Teste WPF verifica atualização na thread da interface, fechamento com painel
  oculto, respeito à desconexão manual e reconexão explícita. Capturas claras e
  escuras revisadas; transporte e processos usados nos testes são isolados.
- Verificados startup fechado/aberto, prontidão demorada, timeout, falha de
  lançamento, processo desconhecido, comunicação perdida, reabertura, configuração
  alterada durante solicitação, serialização e cancelamento no encerramento.
- Nenhum executável Louvor JA foi aberto nem comando de áudio/projeção real foi
  acionado pelos testes. O ciclo completo com a instalação real ainda deve ser
  validado pelo operador. Não houve commit, release ou publicação destas mudanças.

### Validação da busca dinâmica — 01/11/2026

- Suíte Debug completa: **566 testes aprovados**; cobertura total **88,75% de
  linhas e 78,24% de branches**. Isolamento no build Release confirmado.
- Verificados digitação rápida (uma consulta final), resultados/erros antigos,
  campo vazio, pesquisa imediata, conexão ausente, falha, descarte e desconexão.
- Testes WPF digitam no campo real, confirmam que permanece habilitado durante a
  consulta e conferem resultado vazio e nova pesquisa nos temas claro/escuro.
- Guia e página local do site atualizados. A validação com catálogo real fica
  com o operador; testes não acionaram a instalação ou músicas do Louvor JA.

### Validação do opt-in Flute — 02/11/2026

- Implementação real promovida para Debug/Release, desativada por padrão; sem
  rótulo de experimento no menu. Não é compatível com o Louvor JA legado.
- Suíte Release completa: **580 testes aprovados**, cobertura total **89,31% de
  linhas e 78,52% de branches**. Nenhum limite de cobertura foi reduzido.
- Regressão completa em Debug: **580 testes aprovados**, sem falhas ou ignorados.
- Testes isolados conferem JSON legado, preservação cruzada de Enabled/caminho/
  porta, atualizações concorrentes, arquivo bloqueado/corrompido e cancelamento.
- WPF: salvar, descartar pelo diálogo de alterações não salvas, falhar/repetir
  leitura e salvamento, ocultar aba nas regras de busca, alternar rapidamente,
  cancelar prontidão, fechar janela, retornar à Biblioteca e preservar outras
  ferramentas. Desativada não cria View/sessão. Capturas dos temas claro/escuro
  em TestResults/louvor-ja/settings-*.png revisadas.
- Guia, página do site, roadmap e seção Em desenvolvimento do changelog
  atualizados; links locais do site verificados. Versão do app permanece 1.5.0
  até retomar o preparo de release; não houve commit, instalador ou publicação.
- O ensaio do novo checkbox com o Flute real, inclusive desativação com música
  tocando e reinicialização nos dois estados, permanece para aceite do operador.

### Aceite e preparo da 1.6.0 — 02/11/2026

O operador confirmou que o incremento passou nos testes manuais e autorizou
retomar o release da feature. Changelog e guia/site preparados para 1.6.0.
A geração local do instalador e a publicação são verificadas pelo processo de
release; os registros anteriores de pausa/Debug descrevem etapas históricas.

Validação local concluída pelo `releaser.ps1 -Version 1.6.0`: 580 testes Release
aprovados, cobertura total 89,31% de linhas e 78,52% de branches. Instalador
`Sinalo-Setup-win-x64.exe` gerado com 205.270.566 bytes, produto 1.6.0; projeto,
changelog e executável publicados na mesma versão. SHA-256 do instalador local:
`D04FF8910044517E0553DE39A25CBDD8360D17621FBF60738ECBFAC66EFEB23D`.
O artefato distribuído é reconstruído pelo workflow GitHub e terá seu próprio
checksum, verificado na publicação; não se exige hash idêntico entre os builds.

### Publicação confirmada — 02/11/2026

- Implementação: commit `7db0c61`; preparo de release/tag: `ac278e8` / `v1.6.0`.
- CI `41509936189`, Pages `41509936129` e Release `41510506734` aprovados.
- Release pública e latest: https://github.com/fabriciosenadev/sinalo/releases/tag/v1.6.0
- Instalador distribuído: `Sinalo-Setup-win-x64.exe`, 205.238.559 bytes.
- SHA-256 publicado, conferido com o digest do asset GitHub:
  `06cfbfcd5b2f81413aaa130bd5a478b9fb29a6cb6cbfd53b42b1a91f51d67480`.
- Guia acessível em https://fabriciosenadev.github.io/sinalo/louvor-ja.html.
  Sem instalação do pacote na máquina do operador pelo agente; os testes manuais
  da funcionalidade foram aprovados pelo usuário antes da publicação.
