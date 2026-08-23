# Roadmap do Sinalo

## Estado atual

O MVP funcional possui descoberta, sincronização, reprodução local e exclusão para as três fontes:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

O Minuto de Saúde está implementado, validado manualmente e coberto por testes. A descoberta da coleção trimestral, a leitura de datas e títulos e o download para `content\AAAA-TN\health` foram confirmados.

A distribuição inicial também está concluída: o projeto gera instalador self-contained para `win-x64` com Inno Setup, preserva os dados do operador e publica releases pelo GitHub Actions.

## Próxima etapa aprovada

### Atualização automática

O Sinalo verificará periodicamente a GitHub Release mais recente em segundo plano, sem atrasar a abertura da biblioteca. Quando houver uma versão mais nova, exibirá a versão e as notas em português, fará o download do instalador para `%LocalAppData%\Sinalo\updates` e mostrará o progresso.

Após o download e a validação de integridade, o operador terá o botão **Atualizar e reiniciar**. Um atualizador auxiliar encerrará o Sinalo, executará o instalador Inno Setup em modo silencioso com a interface de progresso do Windows e abrirá a nova versão ao final. A confirmação do operador continua obrigatória para instalar; a elevação do UAC do Windows não será contornada. Os dados em `%LocalAppData%\Sinalo`, incluindo vídeos, configurações e catálogo, serão preservados.

## Melhorias mapeadas para depois

Estas melhorias são válidas, mas estão fora do escopo atual e não devem bloquear a distribuição inicial:

- Escolha de outro disco para a pasta de conteúdo.
- Verificação de espaço antes de sincronizações grandes.
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
