# Execução da remodelagem de UX/UI

Data: 08/10/2026. Branch: `feat/experiencia-usuario`.
Base: `main` local, commit `4291646`.

Status: implementação dos planos 00–17 realizada; aceite manual pendente.
Sem merge da Biblioteca geral ou do controle completo MPV, sem alteração de
versão, commit, push, tag, instalador ou release nesta atividade.

## Correspondência entre planejamento e entrega

| Plano | Implementação |
| --- | --- |
| 00 Identidade | `VisualIdentity.xaml`: tipografia explícita, espaços, botões, listas, abas, seleção, foco, expanders, sliders e rolagem; tokens semânticos claros/escuros. |
| 01 Estrutura | Cabeçalho compacto, navegação selecionada, saída compartilhada e workspace contextual, sem lateral global acumulando tarefas. |
| 02 Missões | Lista legível, busca por título/data, resultado vazio específico, detalhes e reprodução explícita na tela escolhida. |
| 03 Provai e Vede | Mesmo contrato visual, resumo da regra e acesso à configuração, preservando política trimestral/semanal. |
| 04 Minuto de Saúde | Mesmo catálogo; adição contextual por link com destino pré-selecionado, sem mudar a fonte automática. |
| 05 Link | Consulta, qualidade e destino/data; formatos invalidados ao mudar URL; ação final e acesso a Downloads fora da rolagem. |
| 06 Cronômetro | View própria, iniciar/pausar/continuar e zerar, campo regressivo condicional, apresentação separada da contagem. |
| 07 Culto/áudio | Contagem e áudio separados em telas largas, configuração recolhível, ajustes em minutos e parar áudio global ao navegar. |
| 08 Sorteio | Cadastro em abas Nomes/Números, rótulos de intervalo, estado ocupado, listas limitadas e confirmação de reinício/remoção. |
| 09 Configurações | Categorias Aparência/Armazenamento/Programas; modos explícitos retrocompatíveis, validação, confirmação de transferência e proteção de mudanças não salvas. |
| 10 Seleção | Lista virtualizada, pesquisa preservando seleção, itens inelegíveis não marcáveis e resumo de tamanhos conhecidos/desconhecidos. |
| 11 Downloads | Janela dedicada ligada à Home atual, quantidade pendente no cabeçalho, progresso indeterminado e cancelamento de todos com confirmação. |
| 12 Diagnósticos | Problema e recomendação separados dos detalhes sanitizados; copiar suporte com feedback. |
| 13 Apresentação | Cena de conferência distinta; área segura, texto adaptável e fundo escuro independente do tema; Esc preservado, sem progresso do cronômetro na saída. |
| 14 Novidades | Versão instalada identificada e expandida; histórico recolhível, em ordem decrescente, sem exibir desenvolvimento como release. |
| 15 Atualização | Painel próprio com versão/estado, confirmação contextual e proteção contra disparos repetidos; protocolo existente preservado. |
| 16 Programação | Janela própria, posições, ações nomeadas e extremos bloqueados; remover não exclui arquivo. Continua em memória, sem autoplay. |
| 17 Mensagens | Diálogos com ação segura padrão, cancelamento/Esc, erros de formulário e feedback contextual; pickers nativos mantidos. |

## Decisões concretas de composição

- **Buscar e baixar** é a ação principal. **Mais opções** concentra seleção
  manual, adição por link, regra e programação; não exige uma lateral fixa.
- Título e ações essenciais não são truncados para caber. Navegação quebra
  nomes longos. Em espaço compacto, a **Tela de saída** é expansível e a faixa
  de sábados deixa de disputar altura com a tarefa principal.
- Detalhes ficam ao lado da lista quando o workspace dispõe de pelo menos
  1000 DIP; abaixo disso, substituem a lista e oferecem **Voltar aos vídeos**.
- Downloads e atualização são painéis distintos, ligados à Home atual mesmo
  quando o catálogo é recarregado. Fechar o painel não cancela o trabalho.
- Navegar não altera a cena aberta no projetor. O indicador global permite
  fechar a apresentação e parar áudio sem procurar controles em outra página.
- Recarregar o catálogo preserva ferramenta, seleção por ID, programação,
  consulta por link, downloads e atualização. Um download novo não rouba a
  seleção ou abre detalhes sem ação do operador.
- Salvar configurações não é uma transação atômica nova. Se a pasta já foi
  transferida e outra etapa falhar, o formulário permanece aberto e informa
  explicitamente essa situação; não há promessa de rollback inexistente.

## Evidências automatizadas e visuais

- `ExperienceLayoutTests`: janelas WPF em STA, temas claro/escuro, 1280×720,
  1920×1080 e 940×580; restrições de espaço com escalas simuladas 125%/150%.
- Verificações de altura útil, ação final do link, seleção, campos e modos
  legados, comandos das Views, áudio global, cenas e rolagem por track/thumb.
- `WorkspaceExperienceTests`: estados/contexto, atualização de seleção,
  programação, novidades e contraste dos textos da faixa de datas.
- Renderizações inspecionadas em `TestResults/ux-ui`; são geradas pelos testes
  e ignoradas pelo Git. Incluem nome longo de sorteado em 1024×768.
- A suíte existente de domínio, integração, sincronização, armazenamento,
  atualização e reprodução continua no ciclo de cobertura de 75%.

Validação final: **288 testes aprovados**, **87,57% de linhas** e **77,05% de
branches**, com `eng/test-coverage.ps1 -Configuration Release`. Build sem avisos
ou erros; meta mantida em 75%, sem reduzir os limiares.
Renderização e doubles não são prova de desempenho no HD, rede, MP3 real,
projetor, leitor de tela ou DPI físico do Windows.

## Roteiro de aceite do operador

1. Abrir os três programas, pesquisar título/data, limpar pesquisa e reproduzir
   pela ação explícita; conferir detalhes em janela pequena e grande.
2. Buscar vídeos, abrir **Mais opções**, selecionar manualmente e acompanhar
   **Downloads** ao navegar para uma ferramenta; cancelar e consultar diagnóstico.
3. Consultar link, alterar URL, conferir qualidade/destino/data e adicionar à
   fila; a confirmação final deve estar acessível sem redimensionar a janela.
4. Abrir configurações antigas sem alterar regras; testar abas, restauração
   de URL, campos inválidos, fechamento com alterações e transferência de pasta
   usando uma coleção de teste — não os arquivos da igreja como primeiro ensaio.
5. Testar cronômetro, áudio de culto real, pausa/parada/volume/posição, ajustes
   de minutos, indicador global e avisos automáticos.
6. Sortear nomes/números, conferir participante longo, reinícios e remoção.
7. Abrir/conferir/fechar apresentação na principal e em monitor secundário;
   navegar entre ferramentas sem trocar involuntariamente a cena do público.
8. Testar Tab, Enter, Espaço, Esc, foco após diálogos e leitor de tela; arrastar
   scroll/slider com mouse físico, clicar no track e usar roda/teclado.
9. Repetir em temas claro/escuro/Windows, DPI real 100/125/150%, computador com
   HD e projetor. Conferir limites da área útil do Windows.
10. Somente após aceite, autorizar commits/integração e planejar a versão.
    Atualização instalada deve ser validada no ciclo de release autorizado.

## Documentação e publicação

### Ajuste após validação do operador

O menu **Mais opções** usa templates próprios, sem a coluna de ícones do
menu nativo, e abre abaixo do botão. **Alterar regra de busca** abre somente
as configurações dos programas; tema e armazenamento continuam exclusivos
do acesso completo por **Configurações**. Há teste de regressão para ambos
os caminhos.

Os botões de ação compartilham altura de 40 px lógicos, largura mínima de
96 px, conteúdo centralizado e espaçamento de 8 px entre ações. Margens e
paddings locais divergentes foram removidos. Navegação mantém altura adaptável
para nomes longos; ícones usam largura compacta. Linhas de campos e botões
foram alinhadas e a suíte verifica geometria nos temas e escalas de layout.

Roadmap, arquitetura, testes, status dos planos e changelog **Em desenvolvimento**
atualizados. Guia do site ajustado nesta branch com aviso explícito de interface
em validação. O Pages existente publica pushes em `main`; nada foi publicado
nesta atividade. Antes de integrar/publicar, remover o aviso de prévia quando
ele deixar de ser verdadeiro e revisar rótulos, capturas e notas da versão.
