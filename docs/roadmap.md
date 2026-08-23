# Roadmap do Sinalo

## Estado atual

O MVP funcional possui descoberta, sincronização, reprodução local e exclusão para as três fontes:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

O Minuto de Saúde está implementado, validado manualmente e coberto por testes. A descoberta da coleção trimestral, a leitura de datas e títulos e o download para `content\AAAA-TN\health` foram confirmados.

## Próxima etapa aprovada

### Atualização automática

O Sinalo consultará a GitHub Release mais recente em segundo plano, baixará o instalador validado e oferecerá ao operador o botão **Atualizar e reiniciar**. A confirmação do operador e a elevação do UAC continuam obrigatórias.

## Melhorias mapeadas para depois

Estas melhorias são válidas, mas estão fora do escopo atual e não devem bloquear a distribuição inicial:

- Escolha de outro disco para a pasta de conteúdo.
- Verificação de espaço antes de sincronizações grandes.
- Limpeza automática trimestral e mensal, respeitando itens fixados e período de tolerância.
- Seleção manual de vídeos específicos antes de sincronizar.
- Miniaturas reais geradas com FFmpeg.
- Logs e diagnósticos mais detalhados para rede, URLs alteradas, disco cheio e arquivos corrompidos.
- Empacotamento e atualização das dependências VLC e FFmpeg/ffprobe.
