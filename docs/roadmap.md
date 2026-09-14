# Roadmap do Sinalo

## Estado atual

O MVP funcional possui descoberta, sincronização, reprodução local e exclusão para as três fontes:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

O Minuto de Saúde está implementado, validado manualmente e coberto por testes. A descoberta da coleção trimestral, a leitura de datas e títulos e o download para `content\AAAA-TN\health` foram confirmados.

A distribuição inicial também está concluída: o projeto gera instalador self-contained para `win-x64` com Inno Setup, preserva os dados do operador e publica releases pelo GitHub Actions. O aplicativo verifica atualizações na abertura e novamente a cada seis horas enquanto estiver aberto, faz o download do instalador e oferece **Atualizar e reiniciar** após a confirmação do operador.

Também já foram concluídos:

- Verificação de espaço livre antes e durante sincronizações, com bloqueio seguro
  quando não houver espaço suficiente.
- Reprodução local com MPV incluído no instalador, fallback para VLC e escolha
  persistida da tela de saída.
- Ferramentas de apresentação: cronômetro simples e sorteio na tela configurada.

## Melhorias mapeadas para depois

Estas melhorias são válidas, mas estão fora do escopo atual e não devem bloquear a distribuição inicial:

- Escolha de outro disco para a pasta de conteúdo.
- Limpeza automática trimestral e mensal, respeitando itens fixados e período de tolerância.
- Seleção manual de vídeos específicos antes de sincronizar.
- Miniaturas reais geradas com FFmpeg.
- Logs e diagnósticos mais detalhados para rede, URLs alteradas, disco cheio e arquivos corrompidos.
- Empacotamento e atualização das dependências VLC e FFmpeg/ffprobe.
- Integração do instalador com assinatura de código, quando houver uma alternativa sustentável.
- Cronômetro de Culto com hora-alvo, ajustes rápidos, progresso e avisos sonoros.
- Bíblia offline e apresentação de passagens:
  - antes de implementar ou distribuir um banco bíblico, identificar a edição e
    confirmar a licença de cada tradução;
  - não reutilizar automaticamente o banco do Desktop nem textos encontrados em
    repositórios de terceiros, pois a licença do arquivo ou do código não
    necessariamente autoriza redistribuir a tradução;
  - para traduções modernas protegidas (como ARA, ARC, NAA, NTLH ou NVI), obter
    autorização escrita que cubra busca, projeção e distribuição offline pelo
    instalador;
  - como alternativa inicial, avaliar uma tradução comprovadamente em domínio
    público ou com licença aberta compatível, exibindo atribuição e edição no
    aplicativo;
  - somente após essa definição, criar o banco SQLite bíblico, busca por
    referência/texto e integração com a tela de apresentação.
- Distribuição confiável no Windows e redução de alertas do SmartScreen:
  - avaliar a publicação na Microsoft Store como opção preferencial sem custo,
    empacotando o aplicativo como MSIX; apps distribuídos pela Store recebem
    assinatura Microsoft;
  - caso o projeto adote uma licença open source elegível, avaliar o SignPath
    Foundation para assinatura gratuita;
  - para computadores administrados pela igreja, considerar uma política interna
    de confiança como alternativa operacional, sem usá-la como solução pública.
