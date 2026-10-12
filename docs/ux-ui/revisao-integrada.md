# Revisão integrada de UX/UI — MPV, Biblioteca e experiência

Data: 11/10/2026. Revisão da entrega 1.2.0. O operador aprovou o teste manual
da rolagem e autorizou a publicação. Isso não substitui os testes físicos
específicos de instalação, projetor e computador com HD.

## Objetivo e critérios

A integração deve parecer um único aplicativo, não três funcionalidades
justapostas. A revisão considera hierarquia, previsibilidade, densidade,
legibilidade, prevenção de erros, contexto da reprodução e acesso às ações.
Marca, configurações existentes, banco legado e arquivos devem ser preservados.

## Revisão por tela

| Tela ou fluxo | Decisão e verificação |
| --- | --- |
| Estrutura e navegação | Biblioteca como ponto de entrada; programa/ferramenta ativa identificados. Navegação rolável, uma escolha de monitor compartilhada e sem retorno da lateral antiga. |
| Biblioteca / Vídeos | Título no shell, sem repetir título e monitor dentro da Biblioteca. Lista virtualizada, origens e datas claras; pesquisa/filtros recolhíveis em espaços compactos. Paginação mantém o total mesmo quando o cabeçalho compacto é ocultado. |
| Biblioteca / Detalhes | Lista e detalhes lado a lado quando há espaço; em janela pequena, detalhes ocupam o centro com Voltar. Reprodução explícita, edição separada, arquivo ausente identificado e remoção com confirmação. |
| Biblioteca / Importação | Aba própria, cópia recomendada e referência ao original explicada. Arquivos/pastas, subpastas, andamento, cancelamento e resultado preservados. Originais não são apagados pela remoção da biblioteca. |
| Reprodução MPV | Painel global independente da navegação, vinculado à sessão real. Não ocupa espaço antes de existir sessão. Pausar/continuar e parar permanecem visíveis; posição, reinício e volume ficam recolhíveis em janela compacta. |
| Vídeos importados no player | ID e nome do importado acompanham a sessão. Nova reprodução usa a Biblioteca, valida o arquivo e atualiza seu histórico; não tenta encontrá-lo apenas no catálogo dos programas. |
| Informativo, Provai e Vede e Minuto de Saúde | Mesma composição, pesquisa nome/data identificada e ações secundárias em Mais opções. Ao abrir um programa, lê os vídeos locais sem consultar a rede. |
| Vídeo por link | Consulta, formato, destino e data separados; confirmação final permanece acessível. Downloads usam o painel comum, sem diálogo de fila diferente para a Biblioteca. |
| Cronômetro | Operação, configuração e apresentação separadas. Não duplica os controles de reprodução. |
| Cronômetro de Culto | Áudio e relógio distintos; parar áudio continua disponível ao navegar. |
| Sorteio | Cadastro em abas, resultados separados e ações destrutivas confirmadas. |
| Configurações | Categorias, validação e ações fixas. O atalho de regra não abre aparência/armazenamento. |
| Downloads e seleção manual | Superfícies comuns para todo o aplicativo; progresso indeterminado explícito, seleção elegível e cancelamento seguro. |
| Diagnósticos e confirmações | Biblioteca usa os diálogos tematizados, com escolha segura e proteção de edição; não introduz MessageBox visualmente diferente. |
| Apresentação | A cena mantém a ferramenta que a abriu; monitor e coordenação com vídeo preservados. Tela pública não ganha controles do operador. |
| Novidades, atualização e programação | Identidade comum e comportamento da entrega de UX mantidos. Programação acessível pela navegação, sem prometer reprodução automática. |

## Ajustes efetivamente aplicados na integração

- Removidas duplicações de título, monitor, mensagens e diálogo de downloads.
- Botões da Biblioteca passaram a usar o padrão compartilhado de altura,
  conteúdo e espaçamento; temas e rolagem vêm da mesma identidade visual.
- Corrigidas áreas com altura zero ou insuficiente em 150% e durante reprodução.
  As ações de maior frequência continuam visíveis; controles avançados são
  recolhíveis, sem alterar a seleção ou os dados ao abrir/fechar.
- Mensagens vazias não consomem uma seção. Status compacto não ocupa várias
  linhas; o texto completo permanece disponível no tooltip.
- Listas usam rolagem por pixels em vez de saltos por item, com atualização
  durante o arraste da barra. Virtualização e reciclagem permanecem ativas;
  a roda usa uma transição de 110 ms com resposta no primeiro frame e destino
  acumulado. Inverter a direção descarta o destino anterior. Barra e teclado
  interrompem a transição; o evento de renderização só fica inscrito durante
  o movimento e é desligado ao concluir ou ocultar/descarregar a região.
  A preferência de animações do Windows é respeitada. Não há I/O de arquivos.
- Programação desta sessão ganhou acesso permanente pela navegação.
- A abertura da Biblioteca carrega dados em segundo plano. Navegar entre
  programas carrega o catálogo local, mantendo ferramentas e seleção ao atualizar.
- Replay de importados, proteção da apresentação e histórico usam os serviços
  corretos, sem duplicar contagens em pausa/continuação.

## Validação

Validação local final da 1.2.0: **353 testes aprovados**, **88,38% de linhas** e
**77,17% de ramificações**, com limiar mantido em 75% para ambas as métricas.
Inclui testes da resposta inicial, acumulação e inversão do scroll, limites,
cancelamento e áreas aninhadas. A rolagem foi aprovada manualmente pelo operador.

Testes renderizam temas claro/escuro em 1280×720, 1920×1080, janelas menores e
escalas simuladas 100%, 125% e 150%. Há cenários com lista cheia, detalhes,
edição, importação e sessão global ativa. Testes de integração usam SQLite,
arquivos temporários e vídeo sintético com MPV; a reprodução não depende de
vídeo remoto. Não foram reduzidos os limites de aceitação nem a cobertura.

Capturas locais ficam em `TestResults/ux-ui`, `TestResults/library-*` e
`TestResults/playback-panel-*`. Não são distribuídas com o aplicativo.

Antes de release, repetir com operador, DPI real, projetor/segundo monitor,
computador com HD, pendrive referenciado, áudio real e teclado/leitor de tela.
Validar também instalação legada, importação interrompida e atualização instalada.

## Publicação

As três entregas e os ajustes combinados commitados foram enviados à main
do GitHub em 11/10/2026 (377094b); PRs #20/#21/#22 mesclados e CI aprovado.
O operador aprovou manualmente a rolagem em 11/10/2026 e autorizou a publicação
da 1.2.0. Ajustes finais, changelog e guia acompanham o commit da versão;
confirmar CI final, tag e instalador pelo workflow antes de comunicar sucesso.
