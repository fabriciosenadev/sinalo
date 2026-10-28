# Minuto de Saúde — fonte secundária do YouTube

Implementação de 28/10/2026, preparada como feature **1.5.0**.
Consulta/interface aprovadas e publicação autorizada pelo usuário.
Instalador e publicação em validação; última versão publicada antes deste ciclo: 1.4.0.
Spec: `../Sinalo-specs/minuto-saude-fonte-secundaria-youtube.md`.

## Entrega

- Configuração opt-in independente do site: verificar canal, habilitar, salvar
  e escolher teto 480/720/1080p, padrão 720p.
- Ação **Consultar vídeos no YouTube** na página Minuto de Saúde. Consulta e
  verificação usam somente metadados. Até quatro publicados por data, não por
  maior episódio; agendados e lives ainda não processadas ficam de fora.
- Todas as caixas inicialmente desmarcadas. Confirmação explícita congela IDs,
  configuração e formatos e enfileira somente selecionados.
- MP4 H.264/AAC sem recodificação implícita; estimativa inclui vídeo/áudio quando
  ambos os tamanhos são conhecidos e sinaliza tamanhos desconhecidos.
- Revalidação antes de executar; já baixados não duplicam. Categoria transferida,
  nome, ID, uso opcional, histórico, fixação e caminho gerenciado são preservados.
- Falta crítica de espaço interrompe os pendentes do lote aprovado. Cancelar a
  consulta não baixa; falhas individuais não são declaradas sucesso total.
- Migração aditiva de configuração e metadados; fontes e regras antigas intactas.
  Publicação não se transforma em sábado de uso. Novos itens fixados por padrão.
- Site, changelog em desenvolvimento, arquitetura, domínio, roadmap e specs
  revisados. Nenhuma atualização automática da ferramenta externa foi adicionada.

## Consulta real — sem mídia

O serviço de produção verificou a identidade `UCufCWU_e8Q_ONyBgf90fZdA`
no canal [Vida e Saúde UCB](https://www.youtube.com/@VidaeSaudeUCB/videos).
O smoke local em `TestResults/health-channel-smoke` retornou:

| Episódio | ID | Publicação | Formato selecionável |
| --- | --- | --- | --- |
| 131 | EWfe7_LvVII | 28/10/2026 | 720p MP4 |
| 130 | XYF2eCjGudE | 21/10/2026 | 720p MP4 |
| 129 | CSw4SA8NMLI | 14/10/2026 | 720p MP4 |
| 128 | SOfGJyJJ_JI | 07/10/2026 | 720p MP4 |

Quatro agendados excluídos; resultado não limitado nessa consulta. Todos os
processos usaram `--skip-download`. Não houve escrita no banco do usuário,
download de mídia, reprodução ou alteração de configurações instaladas.
Disponibilidade é uma evidência desse momento, não garantia futura.

## Testes e limites

- Unitários: URLs/identidade, estados/data, episódio opcional, ordem divergente,
  janela limitada, cache, consulta parcial, codecs/DRM, tamanho dos dois streams,
  configuração alterada, formato/título/tamanho alterado e nova aprovação.
- Integração: SQLite e arquivos sintéticos em diretórios UUID isolados; fila,
  aprovação, deduplicação, transferência de categoria e redownload preservando
  histórico/nome/uso/caminho; pesquisa por publicação e espaço insuficiente.
- WPF automatizado: claro/escuro em 620×550, rodapé visível, botões de 40px,
  cancelar sem requests, confirmar somente selecionado e ação exclusiva de saúde.
  Renders inspecionados em `TestResults/health-channel/light.png` e `dark.png`.
- Revisão de usabilidade: indicador indeterminado durante consulta pendente,
  texto “Buscando…”, ações bloqueadas, estados de resultado/erro e estilos
  compartilhados. Testes com consulta controlada (sem rede) nos dois temas;
  renders de espera `loading-light.png` e `loading-dark.png` no mesmo diretório.
- Suíte final após revisão visual: **510 testes aprovados**, nenhum falhou ou foi ignorado.
  Preparo do instalador 1.5.0 com dependências atuais da main:
  **88,87% de linhas** e **78,27% de branches**, acima do mínimo total de 75%
  para ambos. Comando: `releaser.ps1 -Version 1.5.0`, sem pular os testes.
- Build Release concluído sem avisos; `git diff --check` sem problemas.

## Instalador local

- `releaser.ps1 -Version 1.5.0` concluído com testes e cobertura habilitados.
- Produto do aplicativo/instalador: 1.5.0. Runtime MPV completo (28 arquivos),
  atualizador e componentes de download presentes no publish.
- Instalador local: 205.217.448 bytes; SHA-256 conferido com o arquivo `.sha256`:
  `c845b27390afa0bd3de9a8a964a01cff026698867c41bce4b097beb1436048ea`.
- O artefato do GitHub será reconstruído pela tag e terá seu próprio checksum;
  não é exigido hash idêntico ao build local.

## Roteiro de validação física complementar

1. No build local, abrir Configurações → Programas de vídeo → Minuto de Saúde.
2. Verificar o canal, habilitar a fonte secundária e salvar.
3. No Minuto de Saúde, consultar; conferir os quatro publicados sem agendados.
4. Marcar um e aprovar o download real, quando autorizado pelo usuário.
5. Conferir Downloads, Biblioteca e reprodução offline; repetir a consulta para
   validar “Já baixado”. Conferir transferência de categoria e arquivo ausente.
6. Validar na máquina da igreja com HD/rede precária, incluindo cancelamento e
   pausa durante reprodução. Esses testes físicos **não foram realizados**.

Consulta limitada a 100 entradas, com cache e cancelamento. Não há garantia de
que todo vídeo seja baixável nem fallback automático do site para o canal.
O usuário autorizou a publicação da 1.5.0 após o aceite da consulta/interface.
O teste físico complementar não é apresentado como realizado.
