# Roadmap do Sinalo

## Estado atual

O MVP funcional possui descoberta, sincronização, reprodução local e exclusão para as três fontes:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

O Minuto de Saúde está implementado, validado manualmente e coberto por testes. A descoberta da coleção trimestral, a leitura de datas e títulos e o download para `content\AAAA-TN\health` foram confirmados.

## Próxima etapa aprovada

### Distribuição e instalação

Preparar uma distribuição para os operadores baixarem e instalarem o Sinalo no Windows 11. A primeira entrega usa publicação Release self-contained para `win-x64` e Inno Setup, com instalação em `Program Files`, desinstalação que preserva os dados graváveis e geração local documentada. O GitHub Actions executa CI em pushes e Pull Requests e publica o instalador em uma GitHub Release quando uma tag `vMAJOR.MINOR.PATCH` é enviada. Assinatura de código e atualização automática permanecem fora deste primeiro escopo.

## Melhorias mapeadas para depois

Estas melhorias são válidas, mas estão fora do escopo atual e não devem bloquear a distribuição inicial:

- Escolha de outro disco para a pasta de conteúdo.
- Verificação de espaço antes de sincronizações grandes.
- Limpeza automática trimestral e mensal, respeitando itens fixados e período de tolerância.
- Seleção manual de vídeos específicos antes de sincronizar.
- Miniaturas reais geradas com FFmpeg.
- Logs e diagnósticos mais detalhados para rede, URLs alteradas, disco cheio e arquivos corrompidos.
- Empacotamento e atualização das dependências VLC e FFmpeg/ffprobe.
