# Novidades por versão

Data: 08/10/2026. Criticidade/carga: Baixa · pequena.
Base: main local 4291646; branch feat/experiencia-usuario.

## Tarefa do operador

Reconhecer a versão instalada e mudanças relevantes, sem ler todo o histórico.

## Diagnóstico individual — evidência de código

Arquivos/áreas: ReleaseNotesWindow.xaml e ReleaseNotesParser.
Todas as versões e seções aparecem expandidas; volume cresce. Versão instalada não tem destaque específico e datas concorrem com títulos.

## Proposta de organização e comportamento

- Cabeçalho com versão instalada; versão mais recente disponível no conteúdo e indicação explícita se diferente da instalada.
- Versão instalada inicialmente expandida; histórico em grupos recolhíveis por versão, em ordem decrescente. Datas alinhadas e quebradas no compacto.
- Preservar conteúdo humano do changelog, categorias Novo/Melhorias/Correções e funcionamento offline; não buscar release online para ler novidades locais.
- Não exibir “Em desenvolvimento” como release instalado. Parser continua fonte única, não manter cópia manual dos textos.

## Critérios de aceitação específicos

- Versão atual identificada sem procurar entre várias.
- Ler histórico não depende da internet.
- Changelog sem versão instalada/sem conteúdo tem estado neutro, não falso anúncio.

## Dependências e limites

Componente de grupos, parser e identidade; publicação continua pelo fluxo de release.

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
