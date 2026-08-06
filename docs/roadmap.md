# Roadmap do Sinalo

## Estado atual

O MVP funcional possui descoberta, sincronização, reprodução local e exclusão para as três fontes:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

O Minuto de Saúde está implementado e coberto por testes, mas aguarda **validação manual no aplicativo real** antes de receber commit. Esta validação deve confirmar a descoberta da coleção do trimestre, a leitura de datas e títulos e o download de pelo menos um MP4 para `content\AAAA-TN\health`.

## Próxima etapa aprovada

### Distribuição e instalação

Preparar uma distribuição para os operadores baixarem e instalarem o Sinalo no Windows 11. O escopo será definido antes da implementação e deve incluir publicação Release, versão, instalador, desinstalação e dados graváveis separados da pasta do aplicativo.

## Melhorias mapeadas para depois

Estas melhorias são válidas, mas estão fora do escopo atual e não devem bloquear a distribuição inicial:

- Escolha de outro disco para a pasta de conteúdo.
- Verificação de espaço antes de sincronizações grandes.
- Limpeza automática trimestral e mensal, respeitando itens fixados e período de tolerância.
- Seleção manual de vídeos específicos antes de sincronizar.
- Miniaturas reais geradas com FFmpeg.
- Logs e diagnósticos mais detalhados para rede, URLs alteradas, disco cheio e arquivos corrompidos.
- Empacotamento e atualização das dependências VLC e FFmpeg/ffprobe.
