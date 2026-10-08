# Cronômetro

Data: 08/10/2026. Criticidade/carga: Média · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Controlar tempo rapidamente e distinguir pausar, continuar e zerar da abertura da apresentação.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: IsTimerWorkspace; TimerViewModel.cs.
Tempo de 72 DIP dentro de formulário vertical, botão “Parar/Zerar” ambíguo e contexto externo de vídeos competindo pela atenção. Duração regressiva fica visível mesmo sem ser aplicável.

## Proposta de organização e comportamento

- Tempo e estado no topo, controles principais juntos; configuração em seção secundária.
- “Iniciar”, “Pausar”, “Continuar” conforme estado; explicitar “Zerar” para o comando atual que reseta. Não inventar estado de parada que o serviço não suporta.
- Direção “Crescente/Regressivo”; mostrar campo de duração apenas quando aplicável e validar formato perto do campo antes de iniciar.
- Tela de saída e abrir/fechar apresentação usam o componente compartilhado, não duas cópias de comandos.
- Não criar histórico/tempos anotados, removidos por decisão do usuário.

## Critérios de aceitação específicos

- Abrir/fechar apresentação não implica iniciar/zerar a contagem.
- Pausar/continuar/zerar mantêm comportamento do serviço.
- Tempo principal e controles essenciais visíveis com DPI 150%; configuração pode rolar.

## Dependências e limites

Estrutura e saída compartilhada; independente do Cronômetro de Culto.

## Implementação e verificação

1. Criar o layout e os estados de interface utilizando os componentes de [identidade visual](00-identidade-visual.md).
2. Ligar aos comandos/serviços existentes, sem duplicar persistência ou alterar regras silenciosamente.
3. Cobrir estados vazio, preenchido, ocupado, indisponível e erro aplicáveis; conferir teclado, foco, leitor de tela e ambos os temas.
4. Renderizar em 1280×720 e 1920×1080, testar janela mínima e DPI real 100%, 125% e 150%. Não considerar renderização simulada prova de DPI real.
5. Executar testes unitários, integração e ponta a ponta relevantes e a cobertura total mínima de 75% de linhas e branches.
6. Validar manualmente com o operador e computador com HD. Ao concluir, revisar necessidade de atualizar guia do site, arquitetura, roadmap e changelog; não publicar feature ainda pendente.

Status: implementado em `feat/experiencia-usuario`; aceite manual pendente.
Ver [execução e evidências](execucao.md). Os critérios físicos de HD, projetor,
DPI real e leitor de tela continuam no roteiro de validação do operador.
