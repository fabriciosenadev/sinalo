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

Provai e Vede aceita a página base existente e páginas trimestrais diretas,
convencionais ou em Libras. A página base continua procurando o trimestre atual
convencional; com `#libras` ao final, procura a versão em Libras do trimestre e
ano atuais automaticamente, sem trocar a configuração a cada período. Se essa
versão ainda não estiver publicada na base, informa a ausência sem substituir
por convencional ou trimestre anterior. Um endereço trimestral direto continua
fixo e fornece seu próprio ano e trimestre.
A janela de sábados continua sendo a operacional atual, não a do trimestre fixado.
Versões convencionais preservam os IDs legados; Libras usa IDs distintos e o
sufixo de exibição `(Libras)`. Trocar a URL não apaga os vídeos existentes nem
muda suas categorias. Downloads automáticos e seleção manual desse programa
limitam-se aos itens encontrados no endereço do pedido, não a pendências antigas.

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
- Por solicitação, falhas de inicialização elegíveis permitem até três tentativas
  de MPV antes da contingência. Sucesso ou cancelamento interrompem a sequência;
  erro da mídia não dispara retentativas ou troca automática de player. O
  pré-aquecimento não conta nesse limite e cada solicitação renova seu contador.
- A reprodução sempre usa uma tela de saída selecionada. Uma configuração
  existente é preservada; configurações legadas sem tela definida usam a tela
  principal informada pelo Windows.
- Itens fixados nao sao removidos automaticamente.
- Vídeos adicionados manualmente por link são fixados por padrão e identificados pelo programa e ID estável do vídeo para evitar download duplicado no mesmo destino. O identificador legado `health-youtube-<id>` permanece válido para Minuto de Saúde.
- O trimestre anterior pode ser limpo somente apos sincronizacao completa do novo, respeitando o periodo de tolerancia configurado.

## Biblioteca de vídeos (entrega 1.2.0)

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

## Categorias personalizadas — entrega 1.3.0

Categoria é o destino lógico principal da mídia; origem informa como ela chegou.
Os três programas são categorias de sistema protegidas; Sem categoria preserva
importados legados. Personalizadas aceitam nomes únicos, renomeação e ordenação,
sem conector de descoberta automática. MP4 é o formato desta primeira entrega.

Transferir preserva ID, caminho, modo cópia/referência, metadados, histórico e
fixação. Categoria vazia exige confirmação direta, sem destino obrigatório.
Excluir categoria com mídias exige escolher transferência ou exclusão definitiva:
esta remove cadastros e vínculos internos e apaga somente arquivos gerenciados
próprios. Arquivo referenciado permanece fisicamente intacto. Conteúdo descoberto
pode voltar em sincronização posterior conforme a regra original; não há blacklist.
Limpeza automática não seleciona conteúdo organizado em categorias personalizadas.
Importados não passam a ter limpeza de fonte só porque foram alocados num programa.

## Sessão de reprodução

- Carregamento, reprodução e pausa mantêm a atividade que suspende downloads.
  Parada, término e falha liberam essa atividade; substituir um vídeo não cria
  uma liberação transitória entre os arquivos.
- Uma nova abertura confirmada incrementa o histórico uma vez. Pausar,
  continuar, buscar e reiniciar o mesmo vídeo não criam um novo registro.
- O painel acompanha a mídia ativa, não o cartão selecionado ou o programa
  exibido. A seleção de monitor vale para a próxima abertura, inclusive repetição.
- Sessão, posição, volume e mudo são transitórios; não exigem migração de banco
  nem retomam mídia automaticamente ao iniciar o aplicativo.

## Fila de sincronizacao

### Publicação e aprovação da fonte secundária de saúde

- Canal YouTube é opt-in e independente do site e da seleção de sábados.
- Somente vídeos públicos, publicados e com MP4 H.264/áudio AAC compatível
  participam da consulta. Agendados, lives em andamento e VOD em processamento
  não são inferidos como disponíveis pelo título, episódio ou posição visual.
- Os quatro recentes são ordenados pela publicação confirmada; episódio é
  identificação auxiliar. Já baixado permanece na lista sem permitir seleção.
- Aprovação congela IDs, configuração/revisão, formatos e lote. Atualizar lista
  limpa a seleção. Mudança relevante exige nova aprovação; vídeo novo não entra
  num pedido anterior e selecionado indisponível nunca é substituído.
- Publicação é metadado da origem, não uso no culto. Uso é opcional e editável
  na Biblioteca. O download usa a fila única, sem criar um segundo sincronizador.
- Novos itens são fixados por padrão. Sair dos quatro recentes não remove acervo.
  Remover o arquivo permite redownload aprovado, sem duplicar identidade nem
  desfazer organização. Transferência de categoria não faz o vídeo parecer novo.
- O site vazio ou com erro não dispara download por YouTube: a ação secundária
  continua explícita. Fonte desativada não consulta rede ao iniciar o Sinalo.

- Um pedido pode estar `Waiting`, `Running`, `Completed`, `Failed` ou `Cancelled`.
- Apenas um pedido executa descoberta e download por vez.
- A mesma fonte nao pode estar duas vezes em estados `Waiting` ou `Running`.
- Falha e cancelamento nao tornam nenhum arquivo parcial disponivel offline.
