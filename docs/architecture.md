# Arquitetura do Sinalo

## Objetivo

O Sinalo e um aplicativo Windows para preparar e exibir, sem dependencia de internet durante o culto, videos das fontes Informativo das Missoes, Provai e Vede e Minuto de Saude. O conteudo e sincronizado antecipadamente, organizado por data e trimestre, e reproduzido do disco local.

## Principios

- Operacao offline: a reproducao usa somente arquivos locais validados.
- Inicializacao imediata: nenhuma chamada de rede bloqueia a tela inicial.
- Simplicidade operacional: sem contas, permissao por usuario ou servidor proprio no MVP.
- Conteudo verificavel: um video so fica disponivel apos concluir o download e validar integridade.
- Disco rigido consciente: downloads e miniaturas ocorrem em segundo plano; nunca durante a reproducao.
- Fontes oficiais: o aplicativo consome apenas URLs autorizadas e arquivos disponibilizados pelos respectivos responsaveis.

## Visao geral

```text
URLs das fontes configuradas
          |
          v
Conectores de descoberta ----> catalogo local (SQLite)
                                     |
                                     v
Fila unica de sincronizacao ----> arquivos .part ----> validacao SHA-256
          |                                                |
          v                                                v
      SQLite local <-------------------------- biblioteca local por trimestre
          |
          v
      Interface WPF ----> MPV persistente ----> tela principal / projetor
```

Os conectores identificam os itens publicados nas URLs configuradas e montam um catalogo local. O video e baixado para armazenamento local antes do uso.

## Componentes

### Aplicativo desktop

- **.NET 10 / C#**: runtime e logica do aplicativo.
- **WPF / XAML**: interface nativa Windows, adequada a computadores modestos e multiplos monitores.
- **MVVM com CommunityToolkit.Mvvm**: separa interface, estado e comandos.
- **SQLite**: banco local de catalogo, estado de sincronizacao, configuracoes e historico basico.
- **MPV**: processo externo embutido, pré-aquecido e controlado por IPC para reprodução ágil de arquivos locais. Se não estiver disponível, o Sinalo usa VLC e, por fim, o player padrão do Windows.
- **FFmpeg/ffprobe**: leitura de metadados e geracao sob demanda de miniaturas.

### Descoberta e catalogo local

O MVP nao depende de um catalogo remoto ou de uma API propria. Na tela de configuracoes, o operador informa a URL de cada fonte. Conectores especificos leem essas paginas em segundo plano, identificam os itens publicados e gravam o catalogo no SQLite local.

Cada item descoberto precisa ter data de referencia e uma URL de arquivo autorizada para poder ser baixado. Se a fonte oferecer somente uma pagina ou video online, o item pode ser listado, mas permanecera `OnlineSomente`.

## Estrutura da solucao

```text
Sinalo.sln
src/
  Sinalo.App/              WPF, ViewModels, Views e composicao de DI
  Sinalo.Application/      casos de uso e interfaces
  Sinalo.Domain/           entidades, regras e enums
  Sinalo.Infrastructure/   SQLite, download, arquivos, mpv, FFmpeg e catalogo
tests/
  Sinalo.Tests/
    Unit/                   regras puras e ViewModels
    Integration/            SQLite, armazenamento e conectores
    EndToEnd/               fluxo completo do aplicativo em ambiente isolado
```

As dependencias seguem somente para dentro:

```text
App -> Application -> Domain
Infrastructure -> Application + Domain
```

## Cobertura de testes

O projeto exige cobertura minima de **75% de linhas e branches** nos assemblies de producao. A meta e medida no conjunto da solucao, mas deve ser composta por tres niveis:

- **Unitarios**: regras de dominio, calculos de calendario, selecao e priorizacao.
- **Integracao**: SQLite, sistema de arquivos, downloads, validacao e conectores de fontes, usando diretorios e bancos temporarios.
- **Ponta a ponta**: inicializacao do aplicativo e fluxo operador-configura-fonte-sincroniza-reproduz, em ambiente isolado e sem depender de fontes externas reais.

O script `eng/test-coverage.ps1` executa a suite com a meta de cobertura. Uma alteracao nao esta pronta enquanto esse comando nao passar.

## Modelo de dominio inicial

- `ContentSource`: Missoes, ProvaiEVede ou MinutoDeSaude.
- `ContentItem`: id estavel, titulo, fonte, data sugerida, trimestre, descricao, URL da pagina e lista de arquivos.
- `MediaAsset`: URL de download autorizada, nome local, tamanho esperado, SHA-256, resolucao e duracao.
- `SyncState`: Pendente, Baixando, Validando, Pronto, Falhou ou OnlineSomente.
- `Quarter`: ano e trimestre, por exemplo `2026-T3`.
- `AvailabilityPolicy`: `QuarterlyFull`, `MonthlyFull` ou `RollingSaturday`.
- `StoragePolicy`: limite em GB, trimestre atual, periodo de tolerancia e itens fixados.

Contrato inicial de um item no catalogo:

```json
{
  "id": "missoes-2026-08-08",
  "source": "missions",
  "title": "Informativo Mundial das Missoes - 08/08/2026",
  "scheduledDate": "2026-08-08",
  "quarter": "2026-T3",
  "pageUrl": "https://origem-oficial/exemplo",
  "assets": [
    {
      "id": "video-1080p",
      "downloadUrl": "https://origem-oficial/video.mp4",
      "fileName": "missoes-2026-08-08-1080p.mp4",
      "sizeBytes": 0,
      "sha256": ""
    }
  ]
}
```

Campos de integridade sao obrigatorios antes da distribuicao automatica de qualquer arquivo. Enquanto uma fonte nao fornecer arquivo baixavel autorizado, seu item permanece `OnlineSomente` e nao promete uso offline.

## Politica de disponibilidade por fonte

O Sinalo nao aplica uma unica regra de download a todas as fontes. Cada fonte declara a sua politica no catalogo:

- **`QuarterlyFull`**: baixa todo o trimestre assim que os arquivos oficiais estiverem disponiveis. Esta e a regra do Provai e Vede.
- **`MonthlyFull`**: se todos os videos do mes corrente estiverem publicados e disponiveis, baixa o mes completo de uma vez.
- **`RollingSaturday`**: quando o mes ainda nao estiver completo, mantem uma janela de tres sabados: o anterior, o atual e o proximo. A cada semana, baixa o novo proximo item e remove o mais antigo, salvo se estiver fixado.

O Informativo das Missoes e o Minuto de Saude podem usar `MonthlyFull` quando a fonte disponibilizar todo o mes; caso contrario, usam `RollingSaturday`. O operador sempre pode solicitar a sincronizacao manual de um item ou marcar um item como fixado.

Independentemente da politica da fonte, a rotina operacional calcula tres datas: o sabado anterior, o sabado atual e o proximo sabado. Ela sincroniza nessa ordem: anterior, atual e proximo, desde que cada item ja esteja publicado e tenha arquivo baixavel autorizado. Assim, nao depende de um calendario previamente cadastrado e nunca tenta baixar um video futuro ainda indisponivel.

## Persistencia e arquivos

Dados de aplicacao:

```text
%LocalAppData%\Sinalo\
  data\sinalo.db
  content\2026-T3\missions\missoes-2026-08-08.mp4
  content\2026-T3\provai-e-vede\...
  content\2026-T3\health\...
  cache\thumbnails\...
  temp\downloads\<asset-id>.part
  logs\...
```

Videos nunca sao armazenados no SQLite. O banco guarda caminho, hash, tamanho, estado, ultima verificacao e referencia ao item do catalogo.

O aplicativo sera instalado em `C:\Program Files\Sinalo`, como o MidiaDeck. Dados gravaveis ficam por padrao em `%LocalAppData%\Sinalo`; o operador pode redirecionar somente `content` para outro disco, como `D:\ConteudosSinalo`. Antes da primeira sincronizacao, o app valida permissao de escrita e espaco livre no destino escolhido.

## Sincronizacao

1. O app consulta as fontes configuradas em tarefa de fundo, com timeout curto.
2. Os conectores atualizam o catalogo local com os itens efetivamente publicados.
3. Para fontes configuradas por sábado, aplica as datas selecionadas pelo operador entre anterior, atual e próximo; quando nenhuma data estiver selecionada, usa o trimestre completo. Os itens são enfileirados na ordem anterior, atual e próximo.
4. Enfileira somente arquivos ausentes, alterados ou corrompidos.
5. Baixa para `.part`, com suporte a retomada HTTP Range quando a origem permitir.
6. Valida tamanho e SHA-256; somente entao move o arquivo de modo atomico para `content`.
7. Marca o item como `Pronto` no SQLite e gera miniatura sob demanda.

Downloads devem ter baixa concorrencia (padrao: um por vez), limite configuravel de banda e pausa automatica enquanto houver reproducao.

### Fila de sincronizacao

Os pedidos do operador entram em uma fila unica da sessao. Cada pedido guarda a fonte e sua configuracao no instante em que foi solicitado, atualiza o catalogo e baixa os videos aplicaveis antes de iniciar o proximo pedido. A fila nao aceita a mesma fonte duas vezes enquanto ela estiver aguardando ou em execucao. Uma falha em uma fonte nao interrompe as seguintes; o operador pode cancelar a tarefa ativa e todas as pendentes. A interface mostra fonte, estado, progresso e resultado. Ao fechar o aplicativo, pedidos pendentes nao sao retomados automaticamente.

## Politica trimestral

- Itens `QuarterlyFull` sincronizam primeiro todo o catalogo do novo trimestre.
- A limpeza trimestral so fica elegivel depois que o novo trimestre estiver completo.
- Itens `MonthlyFull` sao removidos ao fim do periodo de retencao mensal configurado.
- Itens `RollingSaturday` mantem somente a janela anterior/atual/proximo por padrao.
- Por padrao, preserva o trimestre anterior por 14 dias apos a sincronizacao completa do novo.
- Itens fixados pelo operador nao sao apagados automaticamente.
- A limpeza respeita um limite configuravel de armazenamento e sempre apresenta o que sera removido.

## Reproducao e telas

- A tela principal e uma biblioteca de uso rapido: `Hoje`, `Proximo Sabado`, fontes e busca.
- Um item `Pronto` abre seu arquivo local no MPV, sem requisicao de rede; na falha dele, o Sinalo usa VLC e depois o player padrao do Windows.
- O MPV permanece ocioso entre vídeos e recebe a troca de arquivo por IPC, reduzindo a espera em computadores com HD. Ele pode abrir em tela cheia no monitor selecionado pelo operador.
- A UI mantem a escolha da tela de saida e registra que a abertura foi iniciada.
- O item pode ter pre-visualizacao em janela do operador, mas a exibicao no projetor e local.

## Fora do MVP

- Login, usuarios e permissao por igreja.
- API propria ou painel web.
- Edicao de catalogo dentro do app.
- Streaming adaptativo e download de plataformas sem arquivo autorizado.
- Estatisticas centralizadas e sincronizacao entre computadores.

## Premissa para sincronizacao offline

O Sinalo so marca um item como offline quando o conector encontrar uma URL de arquivo autorizada, estavel e baixavel. Especialmente no Minuto de Saude, se a fonte continuar oferecendo somente YouTube, o item sera exibido como online ate que exista uma origem oficial de arquivo ou autorizacao para download.
