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
Catalogo remoto versionado (JSON)
          |
          v
Servico de sincronizacao ----> arquivos .part ----> validacao SHA-256
          |                                                |
          v                                                v
      SQLite local <-------------------------- biblioteca local por trimestre
          |
          v
      Interface WPF ----> mpv ----> tela principal / projetor
```

O catalogo remoto informa quais itens existem; ele nao transmite video. O video e baixado para armazenamento local antes do uso.

## Componentes

### Aplicativo desktop

- **.NET 10 / C#**: runtime e logica do aplicativo.
- **WPF / XAML**: interface nativa Windows, adequada a computadores modestos e multiplos monitores.
- **MVVM com CommunityToolkit.Mvvm**: separa interface, estado e comandos.
- **SQLite**: banco local de catalogo, estado de sincronizacao, configuracoes e historico basico.
- **mpv**: processo externo para reproducao de arquivos locais, controlado por IPC.
- **FFmpeg/ffprobe**: leitura de metadados e geracao sob demanda de miniaturas.

### Catalogo remoto

Para o MVP, sera um arquivo JSON estatico hospedado em um endereco HTTPS controlado pelo projeto. Isso evita construir uma API antes de haver necessidade.

O catalogo sera atualizado por curadoria: um processo manual ou automatizado identifica os arquivos oficiais e publica uma nova versao do JSON. O app nao deve raspar paginas HTML durante a execucao do culto.

## Estrutura da solucao

```text
Sinalo.sln
src/
  Sinalo.App/              WPF, ViewModels, Views e composicao de DI
  Sinalo.Application/      casos de uso e interfaces
  Sinalo.Domain/           entidades, regras e enums
  Sinalo.Infrastructure/   SQLite, download, arquivos, mpv, FFmpeg e catalogo
tests/
  Sinalo.Tests/            testes de regras, sincronizacao e persistencia
```

As dependencias seguem somente para dentro:

```text
App -> Application -> Domain
Infrastructure -> Application + Domain
```

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

O catalogo informa a disponibilidade real do arquivo. Assim, o aplicativo nunca tenta baixar um video futuro que ainda nao tenha sido publicado.

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

O local de armazenamento e configuravel. O padrao seguro e `%LocalAppData%\Sinalo`, pois uma instalacao normal em `C:\Program Files\Sinalo` nao deve receber gravacoes durante a operacao: o Windows protege essa pasta e pode exigir privilegio de administrador. Se a igreja quiser uma pasta visivel ao lado do executavel, o instalador deve oferecer um modo portavel ou permitir escolher, por exemplo, `D:\Sinalo\content`; nesse caso, o app valida permissao de escrita e espaco livre antes de sincronizar.

## Sincronizacao

1. O app baixa o manifesto do catalogo em tarefa de fundo, com timeout curto.
2. Compara a versao do catalogo e os hashes com o banco local.
3. Enfileira somente arquivos ausentes, alterados ou corrompidos.
4. Baixa para `.part`, com suporte a retomada HTTP Range quando a origem permitir.
5. Valida tamanho e SHA-256; somente entao move o arquivo de modo atomico para `content`.
6. Marca o item como `Pronto` no SQLite e gera miniatura sob demanda.

Downloads devem ter baixa concorrencia (padrao: um por vez), limite configuravel de banda e pausa automatica enquanto houver reproducao.

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
- Um item `Pronto` abre seu arquivo local no mpv, sem requisicao de rede.
- mpv opera em janela colocada no monitor selecionado, com tela cheia e atalhos de teclado.
- A UI mantem o controle de reproduzir, pausar, parar, volume e selecionar tela.
- O item pode ter pre-visualizacao em janela do operador, mas a exibicao no projetor e local.

## Fora do MVP

- Login, usuarios e permissao por igreja.
- API propria ou painel web.
- Edicao de catalogo dentro do app.
- Streaming adaptativo e download de plataformas sem arquivo autorizado.
- Estatisticas centralizadas e sincronizacao entre computadores.

## Decisao pendente antes de codificar

Definir a origem autorizada dos arquivos de cada fonte, especialmente Minuto de Saude. O Sinalo so implementara sincronizacao offline para arquivos com URL de download permitida e estavel.
