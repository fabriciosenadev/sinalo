# Dominio do Sinalo

## Fontes

| Identificador | Nome exibido | Regra preferencial |
| --- | --- | --- |
| `missions` | Informativo das Missoes | Mensal completo ou janela de sabados |
| `provai-e-vede` | Provai e Vede | Trimestre completo |
| `health` | Minuto de Saude | Mensal completo ou janela de sabados |

As URLs das fontes são configuradas pelo operador para a descoberta automática. Há também uma tela manual de vídeo por link, separada da descoberta trimestral. O operador escolhe Informativo das Missões, Provai e Vede ou Minuto de Saúde como destino e informa a data de uso; o vídeo fica somente na biblioteca escolhida.

## Periodos e disponibilidade

- **Sabado anterior**: sabado imediatamente anterior ao sabado operacional atual.
- **Sabado atual**: o sabado em que a programacao sera usada. Em dias da semana, e o proximo sabado; no proprio sabado, e hoje.
- **Proximo sabado**: o sabado seguinte ao atual.
- **Trimestre**: `AAAA-TN`, onde N e 1 a 4, por exemplo `2026-T3`.
- **Janela de sabados**: conjunto priorizado de anterior, atual e proximo; usada enquanto ainda nao houver lote mensal completo.

## Seleção de downloads

Para Informativo das Missoes, Provai e Vede e Minuto de Saude, o operador define na configuração quais datas
da janela operacional devem ser baixadas: sábado anterior, sábado atual e/ou próximo
sábado. As datas marcadas são baixadas nessa ordem. Quando nenhuma delas está marcada,
o aplicativo baixa o trimestre inteiro.

Instalações criadas antes dessa configuração mantêm o comportamento escolhido: a antiga
opção de janela semanal é migrada como os três sábados marcados; a antiga opção de
trimestre completo permanece como nenhuma data marcada.

A seleção é uma regra de busca, não uma garantia de download: o aplicativo só baixa
itens que forem encontrados na fonte e tenham um arquivo oficial disponível.

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
- A reprodução prioritária usa o MPV incluído no Sinalo e mantido ocioso entre vídeos; VLC e o player padrão são contingências.
- A reprodução sempre usa uma tela de saída selecionada. Uma configuração
  existente é preservada; configurações legadas sem tela definida usam a tela
  principal informada pelo Windows.
- Itens fixados nao sao removidos automaticamente.
- Vídeos adicionados manualmente por link são fixados por padrão e identificados pelo programa e ID estável do vídeo para evitar download duplicado no mesmo destino. O identificador legado `health-youtube-<id>` permanece válido para Minuto de Saúde.
- O trimestre anterior pode ser limpo somente apos sincronizacao completa do novo, respeitando o periodo de tolerancia configurado.

## Biblioteca de vídeos (próxima entrega)

- Vídeos importados têm ID próprio, origem local, tipo MP4, disponibilidade,
  histórico independente e data opcional; não recebem programa ou data fictícios.
- Vídeos sincronizados mantêm ID e histórico originais; o nome personalizado é
  uma sobreposição de exibição, preservada quando o programa é atualizado.
- Cópias gerenciadas ficam em `content/imported/<id>/`; referências continuam no
  caminho original, inclusive quando a pasta gerenciada muda.
- Caminhos repetidos não duplicam cadastros. Nomes iguais em pastas distintas
  podem ser importados. Arquivos ausentes/inválidos não podem ser reproduzidos.
- Limpeza automática aplica-se aos programas, nunca aos importados. Remoção de
  cadastro preserva arquivos por padrão; exclusão explícita limita-se à cópia
  gerenciada sem outra referência. O original referenciado não é apagado.
- Áudio, imagens, organização em coletâneas e fila de reprodução são etapas
  futuras, não funcionalidades desta entrega.

## Fila de sincronizacao

- Um pedido pode estar `Waiting`, `Running`, `Completed`, `Failed` ou `Cancelled`.
- Apenas um pedido executa descoberta e download por vez.
- A mesma fonte nao pode estar duas vezes em estados `Waiting` ou `Running`.
- Falha e cancelamento nao tornam nenhum arquivo parcial disponivel offline.
