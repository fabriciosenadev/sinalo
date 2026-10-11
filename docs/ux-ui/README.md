# Plano de UX/UI de todo o Sinalo

Data: 08/10/2026. Branch independente: `feat/experiencia-usuario`.
Criada diretamente da `main` local, commit `4291646`.
**Implementado em branch; aceite manual pendente.** Não houve merge da Biblioteca
ou player, commit, push, mudança de versão ou release nesta atividade.

Veja o [registro de execução e validação](execucao.md) para a correspondência
entre planos, telas entregues, testes e verificações físicas ainda necessárias.

## Inventário individual

| Plano | Criticidade | Carga |
| --- | --- | --- |
| [00 Identidade e componentes](00-identidade-visual.md) | Alta | Grande, base compartilhada |
| [01 Estrutura principal e navegação](01-estrutura-navegacao.md) | Alta | grande |
| [02 Informativo das Missões](02-informativo-missoes.md) | Alta | média |
| [03 Provai e Vede](03-provai-e-vede.md) | Alta | média |
| [04 Minuto de Saúde](04-minuto-saude.md) | Alta | média |
| [05 Adicionar vídeo por link](05-video-por-link.md) | Alta | média |
| [06 Cronômetro](06-cronometro.md) | Média | média |
| [07 Cronômetro de Culto e áudio](07-cronometro-culto.md) | Alta | grande |
| [08 Sorteio](08-sorteio.md) | Média | média |
| [09 Configurações](09-configuracoes.md) | Alta | grande |
| [10 Escolher vídeos para baixar](10-selecao-downloads.md) | Alta | média |
| [11 Fila de downloads](11-fila-downloads.md) | Alta | média |
| [12 Diagnósticos de sincronização](12-diagnosticos.md) | Alta | pequena/média |
| [13 Tela de apresentação e escolha de saída](13-apresentacao.md) | Alta | média/grande |
| [14 Novidades por versão](14-novidades.md) | Baixa | pequena |
| [15 Atualização do aplicativo](15-atualizacao.md) | Alta | média |
| [16 Programação simples em memória](16-programacao.md) | Média | pequena/média |
| [17 Confirmações e mensagens transversais](17-confirmacoes-erros.md) | Alta | média |

Os três programas têm plano individual e reutilizam layout, não regras idênticas.
Fila, atualização, programação e confirmações são superfícies/fluxos atuais,
não necessariamente janelas novas. Inventário cobre MainWindow workspaces,
SettingsWindow, ReleaseNotesWindow, PresentationWindow e diálogos em C#.
Janelas nativas de pasta/arquivo, MPV/VLC externos e Inno Setup são fronteiras:
validar handoff, mas não redesenhar sua UI como parte do Sinalo.

## Ordem de execução proposta

1. Contrato de identidade/tokens, componentes e shell; validar primeiro com
   um programa e uma ferramenta, antes de propagar.
2. Programas individualmente + detalhes/programação + downloads/seleção/diagnóstico.
3. Configurações, priorizando rolagem, regra retrocompatível e transferência.
4. Cronômetro de Culto e áudio, depois saída de apresentação e cronômetro simples.
5. Vídeo por link e sorteio.
6. Atualização, novidades e auditoria final de todas as superfícies.

Prioridade de operação: player de áudio acessível, saída correta e ações
destrutivas seguras têm precedência sobre refinamentos de cards/Novidades.
Planos 13/15/17 podem antecipar correções críticas; não aguardar etapa estética.

## Marcos e entregas de cada ciclo

Diagnóstico → wireframe/renderização com dados representativos → validação do
operador → componentes/layout → comandos e estados → testes → documentação.
Entregar por commits próprios quando autorizados. Protótipo não é funcionalidade
concluída. Não fazer mudança cosmética apenas para caber numa resolução.

## Métricas de aceitação

Na validação, pedir ao operador: encontrar/reproduzir vídeo por data, entender
catálogo vazio, escolher arquivo para baixar, consultar link, parar áudio,
abrir/fechar apresentação na principal, corrigir regra e identificar update.
Meta: tarefa principal alcançável sem explicar onde está o botão; áudio parado
em até uma ação a partir do indicador quando ativo; nenhuma ação essencial
cortada. Registrar dificuldade/erros e comparar antes/depois; não inventar taxas
de sucesso ou tempos antes de medir.

Ver matriz comum em 00 e critérios individuais em cada arquivo. Renderizações
não substituem uso real em HD, projetor, teclado e DPI real.

## Independência das outras branches

Biblioteca e controle MPV não são pré-requisitos. Seus planos anteriores
permanecem separados em Sinalo-specs; esta branch não inclui seus arquivos.
Após integração futura, comparar identidade/estados, resolver conflitos de
App.xaml/MainWindow/Home sem duplicar seletores ou perder tarefas ativas.
Não modificar planos de feature de outra branch ao implementar esta iniciativa.

## Natureza dos achados

Análise estática baseada no código atual e feedback anterior, não teste de
usabilidade com usuários feito hoje. Propostas de comportamento novo
(confirmação, bloqueio, filtragem) exigem testes e aprovação no ciclo de execução.
