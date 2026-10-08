# Diagnósticos de sincronização

Data: 08/10/2026. Criticidade/carga: Alta · pequena/média.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Entender o problema e a ação indicada sem interpretar uma caixa inteira de texto técnico.

## Diagnóstico individual — evidência de código

Arquivos/áreas: SynchronizationDiagnosticWindow.cs; classificadores existentes.
Conteúdo amigável, etapa e recomendação estão concatenados num TextBox. Hierarquia visual e recuperação são fracas; copiar suporte é útil e deve ser preservado.

## Proposta de organização e comportamento

- Título “Não foi possível concluir o download” ou equivalente conforme etapa; resumo do problema, programa/data, seção “O que fazer”.
- Detalhes técnicos em expander secundário somente dados sanitizados já fornecidos; copiar suporte com retorno acessível.
- Categorias rede, endereço/publicação, espaço/permissão, integridade mostram recomendação do classificador, sem reclassificar só pela UI.
- Fechar retorna ao contexto e mantém fila/seleção. Oferecer “Voltar ao programa” quando útil, sem retry automático inexistente.

## Critérios de aceitação específicos

- Usuário identifica próximo passo sem abrir detalhes técnicos.
- URLs/tokens não são expostos além do conteúdo sanitizado atual.
- Copiar detalhes usa SupportText e informa sucesso/falha sem substituir recomendação.

## Dependências e limites

Fila e classificadores; reutilizar diagnóstico funcional entregue.

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
