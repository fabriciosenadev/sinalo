# Roadmap do Sinalo

**Versão atual:** 1.3.0 — série estável
**Revisado em:** 16/10/2026

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

## Entrega 1.2.0

- **Controle completo do MPV:** pausar/continuar, parar, reiniciar, posição,
  volume e mudo com estado confirmado por IPC; painel global comum a programas,
  Biblioteca e ferramentas. Não inclui fila automática de reprodução.
- **Biblioteca geral — vídeos MP4:** importação por arquivo/pasta com cópia ou
  referência, pesquisa, detalhes, edição protegida, localização de ausentes,
  histórico e remoção segura. Áudio e imagens continuam futuros.
- **UX/UI:** planos 00–17 executados e revisados com MPV e Biblioteca.
  Componentes, monitor, confirmações e downloads compartilhados; ajustes para
  janelas compactas e escala de 150%, inclusive com vídeo ativo.
- [Revisão integrada por tela](ux-ui/revisao-integrada.md). As três entregas
  estão integradas na main local e os commits já foram enviados ao GitHub
  (377094b). A entrega **1.2.0** é minor por acrescentar funcionalidades
  compatíveis. CI da integração aprovado; publicação pelo workflow de release.
- **Acabamento para 1.2.0:** rolagem suave e rápida na roda do mouse, com barra
  direta e virtualização preservada; programação acessível pela navegação.
  Novo teste manual do scroll aprovado pelo operador em 11/10/2026,
  com autorização para publicar a versão.
- Os testes isolados e renders não substituem o aceite conjunto em instalação
  legada, projetor, DPI real e computador com HD. Esses cenários permanecem
  no roteiro de validação operacional; a aprovação do scroll não presume todos eles.

## Entrega 1.3.0

### Destinos e categorias personalizados — implementado

- Estabilização de usabilidade implementada localmente em 16/10/2026: controles
  alinhados, comandos condicionais, confirmação de categoria vazia, escolhas
  explícitas para mídias e tratamento de falhas/reentrância.
- Menu de categorias corrigido e gerenciamento com seleção da categoria aberta
  por ID, sem herdar filtros de outros workspaces. Sem alteração do banco do usuário.
- Erro de thread na confirmação identificado no bootstrap e corrigido com estado
  seguro de ocupação, sem acesso à janela pela tarefa de exclusão.
- 421 testes aprovados; 88,68% linhas / 77,77% branches na rodada final de estabilização.
  Confirmação/cancelamento, bloqueios e nova tentativa cobertos em dados isolados.
  O usuário confirmou que a exclusão funcionou em 16/10/2026 e autorizou publicação.
  Cenários físicos não exercitados continuam no roteiro operacional; ver
  `docs/validacao-estabilizacao-categorias-2026-10-16.md`.

- Planejado em `Sinalo-specs/destinos-categorias-personalizados.md`, K1–K6.
- Criar/renomear/ordenar categorias por nome, com acesso no menu e escolha de
  destino nos fluxos existentes de importação e Vídeo por link. Preservar os três
  programas e suas regras de descoberta; personalizadas não criam novas fontes.
- Separar categoria de origem: transferência conserva IDs, caminhos, metadados,
  histórico, fixação e vínculos, sem copiar/mover fisicamente os arquivos.
- Excluir categoria oferece transferir todas as mídias para outro destino ou
  excluí-las definitivamente: apagar arquivos gerenciados, remover somente
  cadastros/vínculos dos referenciados, nunca os originais externos.
- Confirmar impacto, bloquear conflitos ativos e recuperar falhas parciais por
  journal; remover categoria apenas ao concluir. A descoberta não deve desfazer
  classificação manual e a limpeza precisa respeitar a nova organização.
- Ciclo EK concluído para MP4 na 1.3.0, sem depender de coletâneas/fila,
  áudio/imagens, FFmpeg ou miniaturas. Preserva dados e configurações da 1.2.0.

## Melhorias futuras

### Organização e reprodução completas inspiradas no MidiaDeck — implementação grande, por etapas

O Sinalo já tem catálogo SQLite de vídeos offline, três programas de origem,
MPV persistente, escolha de monitor, fallback para VLC e uma programação simples
em memória. Esses componentes serão preservados. A fila de sincronização atual
é exclusiva de descoberta/download: não deve ser reaproveitada como fila de
reprodução. A evolução abaixo deve manter os vídeos existentes, as configurações
de saída e o funcionamento offline das instalações atuais.

1. **Biblioteca geral e importação local — vídeos entregues na 1.2.0;
   expansão futura, prioridade alta.** Ampliar o
   catálogo para vídeos, áudios e imagens adicionados pelo operador, mantendo
   os três programas como origem/atributo dos vídeos atuais. Definir e mostrar
   se cada importação referencia o arquivo original ou copia para o conteúdo
   gerenciado; detectar arquivos movidos, ausentes ou inválidos. Preservar IDs,
   caminhos, fixação e histórico de reprodução já gravados no SQLite.
   Spec: `Sinalo-specs/biblioteca-geral-importacao-local.md`. A primeira entrega
   cobre vídeos e usa a reprodução atual, sem depender do controle completo do
   MPV; áudio e imagem tornam-se operacionais nos ciclos dos serviços dedicados.
   Expansão agora especificada em `Sinalo-specs/biblioteca-audio.md` e
   `Sinalo-specs/biblioteca-imagens.md`, ambas de carga grande. Áudio depende
   da base das dependências de mídia; imagens não dependem de FFmpeg.
2. **Coletâneas e operação persistente — prioridade alta.** Permitir coletâneas
   criadas pelo usuário, independentes dos programas de origem, com busca,
   filtros e ordenação. Criar uma operação global e ordenável de itens preparados,
   admitindo o mesmo vídeo mais de uma vez. Migrar a programação atual, hoje
   apenas em memória, para esse modelo sem duplicar os arquivos físicos.
   Carga grande; spec `Sinalo-specs/coletaneas-operacao-persistente.md`.
   Coletânea é lista reutilizável; Programação é o snapshot preparado e persistido.
   Depende só da Biblioteca existente, não da futura fila ou de miniaturas.
3. **Controle completo do player — entregue na 1.2.0.**
   Base para evoluções futuras, sem fila automática de reprodução.
4. **Fila de reprodução separada — prioridade alta, depende do player.** Ao
   implementar, usar a base integrada e confirmar seu estado remoto antes de
   criar outra branch; não repetir o desenvolvimento dos contratos. Salvar
   ordem, destino, item atual e estado da fila; oferecer modo manual e automático.
   Avançar automaticamente apenas após término confirmado de mídia iniciada pela
   fila. Validar o arquivo local antes de cada reprodução; imagens devem ficar
   visíveis até uma ação explícita, não avançar como vídeos temporizados.
   Carga grande; spec `Sinalo-specs/fila-reproducao.md`.
   Coletâneas são integração recomendada, não requisito para enfileirar mídia avulsa.
   Reinício não reproduz automaticamente; fallback não controlável opera manualmente.
5. **Roteiro de evento em blocos — prioridade média, depende da biblioteca e da
   reprodução.** Criar eventos, blocos e itens ordenáveis que referenciem mídias
   da biblioteca e ferramentas já existentes (cronômetros e sorteio). Acrescentar
   tipos externos, como PDF, apresentação, site e links, por adaptadores próprios,
   sem tratá-los artificialmente como vídeos. Distinguir o roteiro completo da
   operação rápida e da fila que está efetivamente em execução.
   Carga muito grande; spec `Sinalo-specs/roteiro-evento-blocos.md`.
   R1 cobre MP4 e ferramentas nativas, após programação persistente e integração
   com fila. R2 cobre adaptadores externos com capacidades/limitações explícitas.
   Não exigir áudio/imagens para começar; não avançar ferramentas interativas
   automaticamente nem prometer controle de aplicativo externo que não existe.
6. **Paridade avançada — prioridade condicionada, após o núcleo.** Adicionar
   serviço dedicado para áudio, visualizador persistente de imagens e importação/
   exportação portátil de coletâneas com validação de integridade. Avaliar duas
   saídas simultâneas de vídeo somente após medir CPU, memória e disco no
   computador da igreja; duas instâncias do MPV não são requisito da reprodução
   normal em uma tela.
   Áudio e imagens são as mesmas specs da expansão da Biblioteca, não nova entrega
   duplicada. Pacotes: `Sinalo-specs/pacotes-portateis-coletaneas.md`, carga grande,
   dependem de coletâneas/importação validada. Duas saídas:
   `Sinalo-specs/video-duas-saidas.md`, carga muito grande e benchmark obrigatório
   antes de aprovar a implementação completa; mesmo vídeo em dois monitores.

Planejamento integrado em `Sinalo-specs/organizacao-operacao-midia.md`, revisado
em 12/10/2026. Ordem recomendada: coletâneas/programação persistente → fila →
áudio e imagens → roteiro → pacotes. Duas saídas ficam condicionadas ao resultado
do teste físico. Dependências e miniaturas seguem trilha independente; dependências
também sustentam áudio. Essas propostas estão planejadas, não implementadas.

Execução coordenada em `Sinalo-specs/plano-mestre-execucao.md`: ciclos E0, EK e E1–E10,
benchmark condicional X, responsabilidades compartilhadas e gates de validação,
aceite instalado, documentação, integração e publicação. A ordem padrão não
transforma recomendações em dependências técnicas nem define versões antecipadamente.

O modelo comum mantém IDs de mídias e cria IDs próprios para cada ocorrência;
persistência não inicia reprodução após reinício. Sessões de reprodução, áudio e
apresentação têm responsáveis explícitos para evitar avanço duplicado, som órfão
ou sobrescrita de ferramenta em uso. Arquivos de listas salvas não ficam protegidos
da limpeza para sempre: manter fixação explícita e proteção temporária da operação.

Em cada etapa, criar migrações SQLite reversíveis quando possível, testes
unitários/de integração/de ponta a ponta e validação manual em computador lento.
Não alterar a regra de que só arquivos locais completos e validados aparecem
como prontos para uso offline. A implementação deve preservar a separação
`App -> Application -> Domain`, com `Infrastructure` implementando os contratos.

### Dependências de mídia — prioridade média, implementação grande

- Planejamento em `Sinalo-specs/empacotamento-atualizacao-dependencias-midia.md`.
- MPV completo e FFmpeg de versão fixa já são distribuídos. Centralizar manifesto,
  hashes/origem/avisos, incluir ffprobe do mesmo build e resolver ferramentas por
  caminhos geridos; não duplicar o FFmpeg usado pelos downloads por link.
- VLC continua externo nesta entrega, preservando a contingência; não instalar
  nem atualizar programas externos automaticamente.
- Componentes são atualizados junto do instalador do Sinalo, sem download de
  ferramentas na abertura ou durante o uso. Verificar distribuição/licenças dos
  builds, checkout frio, instalação limpa, atualização legada e operação offline.
- Executar D1–D4 antes da entrega das miniaturas. Não depende de coletâneas,
  programação persistente ou fila de reprodução; preservar o controle MPV atual.

### Miniaturas reais — prioridade baixa, implementação grande

- Gerar miniaturas dos vídeos locais com FFmpeg em segundo plano, mantendo os
  itens funcionais quando a geração falhar. Aplicar à Biblioteca, aos três
  programas e aos detalhes, preservando listas, temas e rolagem da 1.2.0.
- Spec revisada em 12/10/2026: `Sinalo-specs/miniaturas-reais-ffmpeg.md`.
- Cache gerido, extração unitária de baixa prioridade, demanda visível primeiro,
  cancelamento por reprodução/atualização e integração com exclusão, limpeza,
  localização e migração da pasta. Regeneração explícita sem apagar vídeos.
- Executar M1–M6 após a base D1–D4 das dependências, sem depender das demais
  evoluções da Biblioteca. Não declarar concluído só ao gerar JPEG isolado.

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
