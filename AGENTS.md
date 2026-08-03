# Contexto de trabalho do Sinalo

## Produto

Sinalo e um aplicativo desktop Windows para preparar e exibir videos de programacao da igreja. O operador sincroniza previamente conteudos das fontes Informativo das Missoes, Provai e Vede e Minuto de Saude; durante o culto, a reproducao usa arquivos locais validados e nao depende de internet.

## Stack definida

- C# e .NET 10.
- WPF/XAML e MVVM com CommunityToolkit.Mvvm.
- SQLite para dados locais.
- mpv para reproducao e FFmpeg/ffprobe para metadados e miniaturas.
- Instalacao em `C:\Program Files\Sinalo`; dados gravaveis em `%LocalAppData%\Sinalo`, com `content` redirecionavel para outro disco.

## Regras de produto que nao devem ser quebradas

- A UI deve abrir sem bloquear por rede.
- Um video somente pode aparecer como offline quando estiver completo e validado localmente.
- Downloads usam arquivo temporario `.part` e so sao movidos para a biblioteca apos validacao.
- A prioridade de sincronizacao e: sabado anterior, sabado atual e proximo sabado que ja esteja disponivel.
- Provai e Vede usa download trimestral completo quando os arquivos estiverem publicados.
- Missoes e Minuto de Saude usam download mensal completo quando possivel; caso contrario, usam a janela de tres sabados.
- Conteudo de fonte sem arquivo oficial/autorizado deve permanecer `OnlineSomente`; nao implementar bypass ou download nao autorizado de plataformas.
- Links das fontes sao configuraveis pelo operador.

## Qualidade e operacao

- Favorecer baixo uso de CPU, memoria e I/O, pois o computador pode usar HD.
- Operacoes de rede, hashing, leitura de video e miniaturas devem ocorrer fora da thread da interface.
- Pausar ou reduzir downloads enquanto um video estiver em reproducao.
- Tratar rede indisponivel, disco cheio, arquivo corrompido e URL alterada como estados visiveis ao operador.
- Escrever testes para regras de calendario, selecao de itens, integridade e persistencia.

## Fluxo de mudancas

- Leia `docs/architecture.md` e `docs/domain.md` antes de alterar o comportamento do produto.
- Preserve a separacao `App -> Application -> Domain`, com `Infrastructure` implementando contratos de `Application`.
- Nao criar commits sem aprovacao explicita do usuario.
