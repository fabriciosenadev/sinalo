# Roadmap do Sinalo

## Estado atual

O MVP funcional possui descoberta, sincronização, reprodução local e exclusão para as três fontes:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

O Minuto de Saúde está implementado, validado manualmente e coberto por testes. A descoberta da coleção trimestral, a leitura de datas e títulos e o download para `content\AAAA-TN\health` foram confirmados.

A distribuição inicial também está concluída: o projeto gera instalador self-contained para `win-x64` com Inno Setup, preserva os dados do operador e publica releases pelo GitHub Actions. O aplicativo verifica atualizações na abertura e novamente a cada seis horas enquanto estiver aberto, faz o download do instalador e oferece **Atualizar e reiniciar** após a confirmação do operador.

## Melhorias mapeadas para depois

Estas melhorias são válidas, mas estão fora do escopo atual e não devem bloquear a distribuição inicial:

- Escolha de outro disco para a pasta de conteúdo.
- Limpeza automática trimestral e mensal, respeitando itens fixados e período de tolerância.
- Seleção manual de vídeos específicos antes de sincronizar.
- Miniaturas reais geradas com FFmpeg.
- Logs e diagnósticos mais detalhados para rede, URLs alteradas, disco cheio e arquivos corrompidos.
- Empacotamento e atualização das dependências VLC e FFmpeg/ffprobe.
- Integração do instalador com assinatura de código, quando houver uma alternativa sustentável.
- Distribuição confiável no Windows e redução de alertas do SmartScreen:
  - avaliar a publicação na Microsoft Store como opção preferencial sem custo,
    empacotando o aplicativo como MSIX; apps distribuídos pela Store recebem
    assinatura Microsoft;
  - caso o projeto adote uma licença open source elegível, avaliar o SignPath
    Foundation para assinatura gratuita;
  - para computadores administrados pela igreja, considerar uma política interna
    de confiança como alternativa operacional, sem usá-la como solução pública.
