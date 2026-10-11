# Roadmap do Sinalo

**Versão atual:** 1.1.0 — série estável
**Revisado em:** 08/10/2026

Este roadmap separa o que já foi entregue das melhorias futuras. A série 1.x
continua recebendo correções e novas funcionalidades compatíveis.

## Entregue

O Sinalo está em uso com descoberta, sincronização e reprodução offline para as
três fontes configuráveis:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

Também estão concluídos:

- Seleção manual de vídeos publicados antes de enfileirar os downloads, mantendo
  disponível o fluxo automático por configuração.
- Verificação de espaço livre antes e durante a sincronização.
- Diagnósticos de sincronização para falhas de rede, site, acesso, estrutura,
  armazenamento, arquivos e operações locais, com detalhes para suporte e logs
  sanitizados.
- Reprodução local com MPV incluído no instalador, fallback para VLC e escolha
  persistida da tela de saída.
- Cronômetro simples, Cronômetro de Culto com avisos sonoros e controles de áudio,
  sorteio e saída na tela de apresentação configurada.
- Exclusão de vídeos baixados e limpeza automática mensal com período de retenção
  configurável; vídeos fixados são preservados.
- Instalador self-contained para `win-x64`, atualização automática na abertura e
  durante a execução, e publicação de releases pelo GitHub Actions.
- Guia do operador publicado no GitHub Pages.
- Vídeo por link, separado da descoberta automática, com escolha entre os três
  programas, data de uso, qualidades MP4, fila única de downloads e retomada de
  arquivos parciais.

## Implementado e validado — preservado em branch, sem release

- **Controle completo do MPV:** painel de reprodução independente da navegação,
  pausa/continuação, parada, reinício, posição, volume e mudo; estado e término
  confirmados por IPC, histórico após carregamento e coordenação com apresentação.
  A base da fila foi entregue, mas a fila automática continua fora desta etapa.
  O usuário confirmou em 07/10/2026 que o teste realizado funcionou. A validação
  automatizada passou com 288 testes, 86,28% de linhas e 75,72% de branches.
  Implementação, testes, notas em desenvolvimento e ajustes do guia estão na
  branch [`feat/controle-completo-mpv`](https://github.com/fabriciosenadev/sinalo/tree/feat/controle-completo-mpv).
  **Envio remoto pendente:** a branch e os commits estão salvos localmente.
  Em 07/10/2026, o GitHub rejeitou os pushes da branch e do roadmap da `main`
  com `Internal Server Error`; repetir os envios sem force push quando o serviço
  aceitar novamente. O link acima identifica o destino remoto planejado.
  Por decisão do usuário, a publicação foi adiada, sem nova versão definida.
  O código não foi integrado à `main`; nesta branch principal permanece apenas
  este registro do roadmap. Não criar tag/release nem incluir o recurso em
  versões da `main` até autorização para integrar a branch.
  Antes da publicação futura, confirmar os cenários de dois monitores,
  computador com HD e instalação/atualização que não tenham sido cobertos pelo
  teste informado. Spec: `Sinalo-specs/controle-completo-mpv.md`.

## Melhorias futuras

### Experiência e identidade visual de todo o aplicativo — implementada em branch, aceite manual pendente

- Branch `feat/experiencia-usuario`, criada da `main` local (`4291646`), sem
  dependência ou integração das branches da Biblioteca e do controle completo MPV.
- Análise estática individual das telas e fluxos existentes: programas, vídeo
  por link, cronômetros, áudio, sorteio, configurações, seleção manual, downloads,
  diagnósticos, apresentação, novidades, atualização, programação e confirmações.
- [Índice dos planejamentos UX/UI](ux-ui/README.md), com criticidade, carga,
  ordem de execução e critérios por tela; [identidade visual comum](ux-ui/00-identidade-visual.md).
- Implementação grande, por ciclos: componentes e navegação primeiro, depois
  tarefas contextuais. Preservar marca, temas, monitor compartilhado, operação
  offline, compatibilidade das configurações e segurança dos arquivos.
- Planos 00–17 implementados: componentes comuns, navegação, tarefas contextuais,
  diálogos dedicados, configurações por categoria e saída do público separada.
  [Registro de execução e validação](ux-ui/execucao.md).
- Testes automatizados e renderizações cobrem temas, janela mínima, escalas
  simuladas, estados e controles. O aceite manual do operador, incluindo HD,
  projetor e DPI real, permanece pendente; não declarar release entregue.
- Documentação, guia do site e changelog em desenvolvimento preparados nesta
  branch. Sem versão nova, commit ou publicação automática nesta atividade.

### Organização e reprodução completas inspiradas no MidiaDeck — implementação grande, por etapas

O Sinalo já tem catálogo SQLite de vídeos offline, três programas de origem,
MPV persistente, escolha de monitor, fallback para VLC e uma programação simples
em memória. Esses componentes serão preservados. A fila de sincronização atual
é exclusiva de descoberta/download: não deve ser reaproveitada como fila de
reprodução. A evolução abaixo deve manter os vídeos existentes, as configurações
de saída e o funcionamento offline das instalações atuais.

1. **Biblioteca geral e importação local — base, prioridade alta.** Ampliar o
   catálogo para vídeos, áudios e imagens adicionados pelo operador, mantendo
   os três programas como origem/atributo dos vídeos atuais. Definir e mostrar
   se cada importação referencia o arquivo original ou copia para o conteúdo
   gerenciado; detectar arquivos movidos, ausentes ou inválidos. Preservar IDs,
   caminhos, fixação e histórico de reprodução já gravados no SQLite.
   Spec: `Sinalo-specs/biblioteca-geral-importacao-local.md`. A primeira entrega
   cobre vídeos e usa a reprodução atual, sem depender do controle completo do
   MPV; áudio e imagem tornam-se operacionais nos ciclos dos serviços dedicados.
2. **Coletâneas e operação persistente — prioridade alta.** Permitir coletâneas
   criadas pelo usuário, independentes dos programas de origem, com busca,
   filtros e ordenação. Criar uma operação global e ordenável de itens preparados,
   admitindo o mesmo vídeo mais de uma vez. Migrar a programação atual, hoje
   apenas em memória, para esse modelo sem duplicar os arquivos físicos.
3. **Controle completo do player — implementado e validado em branch.** Base de
   controle concluída em `feat/controle-completo-mpv`; integração e publicação
   adiadas por decisão do usuário, conforme seção acima.
4. **Fila de reprodução separada — prioridade alta, depende do player.** Ao
   implementar, trabalhar sobre a branch do controle ou integrar essa base com
   autorização; os contratos novos ainda não estão disponíveis na `main`. Salvar
   ordem, destino, item atual e estado da fila; oferecer modo manual e automático.
   Avançar automaticamente apenas após término confirmado de mídia iniciada pela
   fila. Validar o arquivo local antes de cada reprodução; imagens devem ficar
   visíveis até uma ação explícita, não avançar como vídeos temporizados.
5. **Roteiro de evento em blocos — prioridade média, depende da biblioteca e da
   reprodução.** Criar eventos, blocos e itens ordenáveis que referenciem mídias
   da biblioteca e ferramentas já existentes (cronômetros e sorteio). Acrescentar
   tipos externos, como PDF, apresentação, site e links, por adaptadores próprios,
   sem tratá-los artificialmente como vídeos. Distinguir o roteiro completo da
   operação rápida e da fila que está efetivamente em execução.
6. **Paridade avançada — prioridade condicionada, após o núcleo.** Adicionar
   serviço dedicado para áudio, visualizador persistente de imagens e importação/
   exportação portátil de coletâneas com validação de integridade. Avaliar duas
   saídas simultâneas de vídeo somente após medir CPU, memória e disco no
   computador da igreja; duas instâncias do MPV não são requisito da reprodução
   normal em uma tela.

Em cada etapa, criar migrações SQLite reversíveis quando possível, testes
unitários/de integração/de ponta a ponta e validação manual em computador lento.
Não alterar a regra de que só arquivos locais completos e validados aparecem
como prontos para uso offline. A implementação deve preservar a separação
`App -> Application -> Domain`, com `Infrastructure` implementando os contratos.

### Dependências de mídia — prioridade média

- Definir uma política controlada para empacotar e atualizar VLC e FFmpeg/ffprobe.
  Hoje o MPV é incluído no instalador; VLC é alternativa quando já está instalado.
- A decisão sobre FFmpeg deve preceder as miniaturas reais para que instalação,
  atualização e diagnóstico da dependência tenham um comportamento definido.

### Miniaturas reais — prioridade baixa, implementação grande

- Gerar miniaturas dos vídeos locais com FFmpeg em segundo plano, mantendo os
  cartões funcionais quando a geração falhar.
- Há uma especificação detalhada em `Sinalo-specs/miniaturas-reais-ffmpeg.md`.

### Bíblia offline e apresentação de passagens — prioridade ainda não definida

- Primeiro identificar uma edição e obter confirmação de que sua licença permite
  busca, projeção e redistribuição offline pelo instalador.
- Não reutilizar automaticamente banco/texto do Desktop ou conteúdo de terceiros
  sem confirmar a licença da tradução.
- Após resolver a licença, especificar armazenamento, busca por referência/texto
  e integração com a tela de apresentação.

### Distribuição confiável no Windows — depende de decisão externa

- Avaliar alternativas sustentáveis para reduzir avisos do SmartScreen, incluindo
  Microsoft Store/MSIX ou assinatura gratuita caso o projeto atenda aos critérios
  de uma iniciativa de assinatura.
- Manter a decisão em aberto até confirmar requisitos, elegibilidade e custos.

## Itens fora do roadmap ativo

Não há outra fonte oficial confirmada para o Minuto de Saúde além da coleção
trimestral já integrada. Uma nova fonte só deve ser planejada se houver uma fonte
oficial identificável e conteúdo que o Sinalo possa acessar.

## Revisão ao encerrar uma implementação

Ao concluir cada melhoria, atualizar este roadmap e verificar se o guia público,
as notas de versão ou outras especificações também precisam de atualização.
