# Sorteio

Data: 08/10/2026. Criticidade/carga: Média · média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Adicionar participantes, sortear sem repetição e compreender resets sem perder cadastros por engano.

## Diagnóstico individual — evidência de código

Arquivos/áreas: MainWindow.xaml: IsRaffleWorkspace; RaffleViewModel.cs e RaffleSession.
Nomes grandes usam ellipsis; campos numéricos não têm início/fim individuais. Ações “Reiniciar exibição”, “Reiniciar sorteio” e “Zerar” próximas, sem explicação do efeito. Listas crescem dentro do mesmo formulário.

## Proposta de organização e comportamento

- Resultado em destaque com quebra limitada/adaptação tipográfica para nomes longos; status e contagens sempre juntos.
- Cadastro separado em modos “Nomes” e “Números”, preservando o conjunto existente ao trocar o formulário. Campos “De” e “Até”, exemplos e validação inline.
- “Sortear” como ação principal. Durante animação, estado visível e novos disparos bloqueados sem perder a conclusão.
- Rótulos propostos: “Limpar resultado” (reset só da exibição), “Reiniciar sorteio” (participantes elegíveis de novo), “Remover participantes” (clear); confirmar efeitos lendo/testando RaffleSession antes de substituir rótulos.
- Ações destrutivas separadas; confirmação para remover participantes/reiniciar com histórico de sorteados.
- Disponíveis e sorteados em listas com altura útil/virtualização ou abas compactas; não esticar infinitamente a página.

## Critérios de aceitação específicos

- Cadastro/intervalos funcionam com teclado; início/fim e erros não são ambíguos.
- Múltiplos cliques não prendem animação; número e nome concluem igualmente.
- Ações de reset explicam o que será preservado e nenhum participante é perdido por troca de modo.

## Dependências e limites

Estado do sorteio e saída compartilhada; não adicionar importação externa ou persistência nova.

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
