# Identidade visual e contrato de experiência do Sinalo

Data: 08/10/2026. Status: implementado em branch; aceite manual pendente.
Base: main local 4291646; branch feat/experiencia-usuario. Nenhum merge da Biblioteca ou do controle completo do MPV.

## Critério de análise

Inspeção estática de XAML, janelas montadas em C#, ViewModels, handlers e paletas,
somada ao histórico de feedback do operador. Achados de código são evidência;
propostas de layout são hipóteses a validar. Não foi realizada nesta etapa uma
nova sessão de uso ou auditoria visual de todas as telas em execução.
Não declarar conformidade de acessibilidade ou desempenho sem medição.

Público: operador voluntário, pouco tempo para aprender, computador com HD,
rede instável e projetor. Tarefas: preparar antes do culto, operar durante e
manter instalação. Priorizar clareza e recuperação, não decoração.

## Identidade comum

- Preservar logo/ícones existentes, azul profundo e verde-petróleo. Não mudar marca.
- Recursos semânticos: fundo, superfície, superfície elevada, texto principal/secundário,
  borda, foco, ação primária, sucesso, atenção, erro e seleção.
- Corrigir contraste da faixa azul no tema claro: hoje Brush.Header é #16324F
  enquanto TextPrimary também é #16324F; texto secundário #506273 sobre esse
  fundo tem risco evidente. Criar HeaderText/StatusHeaderText e validar contraste.
- Claro e escuro são versões da mesma hierarquia, não layouts distintos. Seguir
  Windows continua sendo escolha persistida; usar DynamicResource também em diálogos.
- Tipografia Segoe UI/sistema: título 24–28 DIP, seção 18, corpo/controle 14,
  auxiliar 12–13 somente quando não essencial. Evitar informações essenciais em 11.
- Escala de espaço 4/8/12/16/24/32 DIP; entre seções 24, controles 8–12;
  borda 1, cantos 6–10. Menos bordas aninhadas e separadores redundantes.
- Botões/inputs essenciais pelo menos 36 DIP, preferencialmente 40; foco visível,
  sem deslocamento de layout quando ganha borda. Uma ação principal por grupo de tarefa.
- Cor nunca sozinha: estado tem palavra e, quando útil, ícone. Disabled tem motivo
  próximo e não fica tão apagado que o usuário não sabe o que está bloqueado.
- Scrollbar automática, com respiro mínimo 10–12 DIP do conteúdo; testar thumb,
  track/página, roda e teclado. Não substituir funcionalidade por estilização.
- Não usar glifo de play como falsa miniatura; miniaturas reais são outra feature.

## Composição adaptativa

Cabeçalho compacto → navegação persistente → workspace com título → tarefa → resultado.
Navegação larga 220–240 DIP; reduzível a 180–200 apenas com textos legíveis e
sem ícones sem nome. Área principal flexível; não preservar coluna direita vazia.
Detalhes laterais só quando sobrar largura útil; referência inicial: workspace
>=1000 DIP com painel 320–360 e lista suficiente. Abaixo, detalhes em visão
central com Voltar. Valores são ponto de partida, confirmar pelos testes.
Não escalar fontes para compensar layout inadequado nem obrigar maximização.
Modais respeitam WorkArea, título/rodapé fixos e conteúdo rolável.
Evitar scroll dentro de scroll para a mesma tarefa; listas e formulários podem
ter regiões distintas quando ações importantes continuarem acessíveis.

## Contrato de estados e linguagem

- “Programa” é Informativo das Missões, Provai e Vede ou Minuto de Saúde.
- “Fonte/endereço oficial” é URL da descoberta dentro de Configurações.
- “Vídeo” é mídia; “Downloads” é operação de obter arquivos;
  “Programação desta sessão” é lista em memória, não fila automática.
- “Tela de saída” é o monitor compartilhado; “Apresentação” é cena de ferramenta.
- Estado: Pronto para reproduzir / Aguardando / Baixando / Validando /
  Nenhum vídeo novo / Falhou / Cancelado. Preservar enum e significado de domínio.
- Feedback responde: o que aconteceu, o que permanece seguro e o que fazer.
- Pesquisa vazia, catálogo vazio e fonte sem publicação têm mensagens diferentes.
- Rede/disco não bloqueiam UI; operação em fundo visível ao mudar de tela.

## Componentes a construir antes das telas

Tokens e tipografia; cabeçalho/workspace; navegação selecionada; seletor de monitor
compartilhado; formulário com label/ajuda/erro; botões; estado vazio; aviso;
badge; lista virtualizada; painel contextual; diálogo; indicador de tarefa/fila;
grupo de configurações e rodapé de ações. Extrair Views/UserControls e estados
por responsabilidade, sem reescrever serviços ou consolidar tudo no code-behind.

## Aceitação comum

Alvo de contraste: 4,5:1 para texto normal e 3:1 para texto grande/componentes
essenciais, medido por tema; isso não equivale a certificação de acessibilidade.
Teclado completo, Tab lógico, Escape seguro, AutomationProperties.Name/HelpText,
foco que retorna após diálogo, área clicável, nomes longos e leitor de tela.
Sem truncar ação essencial ou requerer rolagem horizontal em janela suportada.
Testar mínimo atual 940×580 e 1280×720/1920×1080, DPI real100/125/150. Se o
mínimo não couber na WorkArea, ajustar restrições sem abandonar acessibilidade.
Lista cheia, vazia, erros, download ativo, áudio ativo, monitor ausente,
dois monitores, temas claro/escuro/Windows. Nenhum hash/rede no layout.

## Limites e validação do usuário

Não incluir Biblioteca geral, importação local, controle MPV completo,
fila de reprodução automática, novas APIs, mudança de banco ou novo release.
Compatibilidade visual futura será verificada após merges autorizados, sem
cherry-pick escondido. Mockups/renderizações com dados de teste precedem
implementação de cada grupo; aceite do operador em 1280×720 e máquina da igreja
fecha cada etapa. Guia/site/changelog revisados ao final de todo ciclo.
