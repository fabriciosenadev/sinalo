# Configurações

Data: 08/10/2026. Criticidade/carga: Alta · grande.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Encontrar um grupo de configuração, entender efeitos e salvar sem perda ou surpresa na mudança de pasta.

## Diagnóstico individual — evidência de código

Arquivos/áreas: SettingsWindow.xaml e SettingsWindow.xaml.cs.
Tema, aviso e conteúdo local ocupam região Auto fora do ScrollViewer; restringem área restante. Subtítulo só menciona fontes embora inclua tema e armazenamento. Trimestre é checkbox derivado da ausência de sábados, uma escolha difícil de descobrir. Save_Click migra arquivos antes de concluir salvamento.

## Proposta de organização e comportamento

- Janela com navegação interna “Aparência”, “Armazenamento”, “Programas de vídeo”; grupos no mesmo modelo editável, sem exigir salvar antes de alternar categoria.
- Cabeçalho e rodapé pequenos fixos; todo conteúdo do grupo em região estrela rolável. Ajustar janela ao WorkArea, não exigir altura 800 em telas baixas.
- Programas usam componente idêntico de URL/restaurar/regra. Modo explícito “Sábados escolhidos/Trimestre inteiro”; sábados multisseleção só no primeiro. Estado inicial interpreta exatamente seleção persistida, preservando ausência como trimestre e compatibilidade legada. Não remapear vazios silenciosamente.
- Aviso de busca não garantir download visível na categoria Programas, próximo da regra; não repetir alerta enorme em categorias sem relação.
- Armazenamento mostra pasta atual, pasta proposta e aviso de transferência. Antes de mover, resumo/confirmar; durante salvamento, “Transferindo vídeos…” e controles bloqueados para evitar duplo save.
- Validar URLs, dias/tolerância e escolha de pasta antes de ação destrutiva; falha mantém formulário e comunica o que foi ou não salvo. Identificar eventual necessidade de orquestração na Application como subatividade, não prometer atomicidade inexistente.
- Fechar com mudanças pede salvar/descartar/cancelar; Restaurar URL não salva sem confirmação final.

## Critérios de aceitação específicos

- Todos os grupos acessíveis em altura mínima e DPI 150%, Salvar/Cancelar fixos.
- Preferências antigas continuam idênticas após abrir/salvar sem alterações.
- Falha de migração mantém caminho anterior ou informa recuperação conforme contrato real.
- Trocar categoria não perde alterações; nenhum campo inválido é salvo silenciosamente.

## Dependências e limites

Componentes e formularização; melhorias de salvamento podem exigir lógica de Application separada de layout. Não depender da Biblioteca.

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
