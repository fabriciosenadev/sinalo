# Roadmap do Sinalo

**Versão atual:** 1.0.0 — série estável
**Revisado em:** 24/09/2026

Este roadmap separa o que já foi entregue das melhorias futuras. A série 1.x
continua recebendo correções e novas funcionalidades compatíveis.

## Entregue

O Sinalo está em uso com descoberta, sincronização e reprodução offline para as
três fontes configuráveis:

- Provai e Vede.
- Informativo das Missões.
- Minuto de Saúde, usando a coleção trimestral oficial de downloads.

Também estão concluídos:

- Seleção manual de vídeos publicados antes de enfileirar os downloads, mantendo
  disponível o fluxo automático por configuração.
- Verificação de espaço livre antes e durante a sincronização.
- Diagnósticos de sincronização para falhas de rede, site, acesso, estrutura,
  armazenamento, arquivos e operações locais, com detalhes para suporte e logs
  sanitizados.
- Reprodução local com MPV incluído no instalador, fallback para VLC e escolha
  persistida da tela de saída.
- Cronômetro simples, Cronômetro de Culto com avisos sonoros e controles de áudio,
  sorteio e saída na tela de apresentação configurada.
- Exclusão de vídeos baixados e limpeza automática mensal com período de retenção
  configurável; vídeos fixados são preservados.
- Instalador self-contained para `win-x64`, atualização automática na abertura e
  durante a execução, e publicação de releases pelo GitHub Actions.
- Guia do operador publicado no GitHub Pages.

## Melhorias futuras

### Dependências de mídia — prioridade média

- Definir uma política controlada para empacotar e atualizar VLC e FFmpeg/ffprobe.
  Hoje o MPV é incluído no instalador; VLC é alternativa quando já está instalado.
- A decisão sobre FFmpeg deve preceder as miniaturas reais para que instalação,
  atualização e diagnóstico da dependência tenham um comportamento definido.

### Miniaturas reais — prioridade baixa, implementação grande

- Gerar miniaturas dos vídeos locais com FFmpeg em segundo plano, mantendo os
  cartões funcionais quando a geração falhar.
- Há uma especificação detalhada em `Sinalo-specs/miniaturas-reais-ffmpeg.md`.

### Bíblia offline e apresentação de passagens — prioridade ainda não definida

- Primeiro identificar uma edição e obter confirmação de que sua licença permite
  busca, projeção e redistribuição offline pelo instalador.
- Não reutilizar automaticamente banco/texto do Desktop ou conteúdo de terceiros
  sem confirmar a licença da tradução.
- Após resolver a licença, especificar armazenamento, busca por referência/texto
  e integração com a tela de apresentação.

### Distribuição confiável no Windows — depende de decisão externa

- Avaliar alternativas sustentáveis para reduzir avisos do SmartScreen, incluindo
  Microsoft Store/MSIX ou assinatura gratuita caso o projeto atenda aos critérios
  de uma iniciativa de assinatura.
- Manter a decisão em aberto até confirmar requisitos, elegibilidade e custos.

## Itens fora do roadmap ativo

Não há outra fonte oficial confirmada para o Minuto de Saúde além da coleção
trimestral já integrada. Uma nova fonte só deve ser planejada se houver uma fonte
oficial identificável e conteúdo que o Sinalo possa acessar.

## Revisão ao encerrar uma implementação

Ao concluir cada melhoria, atualizar este roadmap e verificar se o guia público,
as notas de versão ou outras especificações também precisam de atualização.
