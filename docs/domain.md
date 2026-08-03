# Dominio do Sinalo

## Fontes

| Identificador | Nome exibido | Regra preferencial |
| --- | --- | --- |
| `missions` | Informativo das Missoes | Mensal completo ou janela de sabados |
| `provai-e-vede` | Provai e Vede | Trimestre completo |
| `health` | Minuto de Saude | Mensal completo ou janela de sabados |

As URLs de pagina/canal de cada fonte sao configuradas pelo operador. A fonte pode ser consultada para descobrir itens publicados, mas somente arquivos oficiais e autorizados podem ser sincronizados para uso offline.

## Periodos e disponibilidade

- **Sabado anterior**: sabado imediatamente anterior ao sabado operacional atual.
- **Sabado atual**: o sabado em que a programacao sera usada. Em dias da semana, e o proximo sabado; no proprio sabado, e hoje.
- **Proximo sabado**: o sabado seguinte ao atual.
- **Trimestre**: `AAAA-TN`, onde N e 1 a 4, por exemplo `2026-T3`.
- **Janela de sabados**: conjunto priorizado de anterior, atual e proximo; usada enquanto ainda nao houver lote mensal completo.

## Estados de sincronizacao

| Estado | Significado |
| --- | --- |
| `Pending` | Conhecido pelo catalogo, ainda nao enfileirado ou baixado. |
| `Downloading` | Arquivo sendo salvo temporariamente como `.part`. |
| `Validating` | Download concluido; tamanho e hash estao sendo verificados. |
| `Ready` | Arquivo local completo, validado e pronto para reproduzir offline. |
| `Failed` | Houve falha de rede, espaco, integridade ou leitura. |
| `OnlineOnly` | Item listado, mas sem arquivo oficial/autorizado para uso offline. |

## Regras de armazenamento

- Executaveis e dependencias sao instalados em `C:\Program Files\Sinalo`.
- Banco SQLite, logs, cache e configuracoes usam `%LocalAppData%\Sinalo`.
- A pasta de conteudo pode ser escolhida pelo operador, especialmente quando houver unidade com mais espaco.
- Videos nao sao gravados no SQLite.
- Itens fixados nao sao removidos automaticamente.
- O trimestre anterior pode ser limpo somente apos sincronizacao completa do novo, respeitando o periodo de tolerancia configurado.
