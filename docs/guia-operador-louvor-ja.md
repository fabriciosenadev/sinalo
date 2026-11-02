# Louvor JA Flute pelo Sinalo — guia do operador

> Disponível a partir do Sinalo **1.6.0**, desativada por padrão. A integração é
> exclusiva do **Louvor JA Flute**, projeto `louvorja/flute-app`, testado na versão
> 2.0.2. Não é compatível com o Louvor JA legado. Neste guia, "Louvor JA" significa
> a aplicação Flute.

## Antes de começar: habilitar no Sinalo

1. Abra **Configurações > Integrações**.
2. Marque **Habilitar integração com Louvor JA Flute** e clique **Salvar configurações**.
3. A opção **Louvor JA Flute** aparecerá na navegação sem precisar reiniciar.

Por padrão a integração fica desativada: não aparece no menu e não consulta
processos, servidor ou catálogo do Flute. Habilitar não instala o Flute, não ativa
o servidor externo e não inicia músicas. Você pode habilitar antes de ajustar o
caminho do executável e a porta na tela da integração.

Desmarcar e salvar remove o menu e cancela conexão, buscas e monitoramento. Se
estiver na tela Flute, o Sinalo retorna à Biblioteca; outras telas permanecem.
**Isso não fecha o Flute nem para músicas em execução.** Caminho e porta ficam
preservados ao reabilitar e entre execuções. Cancelar as Configurações não aplica
a escolha. Usuários do experimento anterior precisam habilitar uma vez; os
caminhos e portas já salvos não são perdidos.

## 1. O que instalar e preparar

Sinalo e Louvor JA precisam estar instalados na **mesma máquina**. A integração
foi analisada/testada com Louvor JA 2.0.2. Instalar apenas o Sinalo não instala,
atualiza nem baixa músicas do Louvor JA. Prepare/sincronize no Louvor JA o acervo
que será usado antes do culto. A comunicação entre os programas é local; a
preparação do acervo pode precisar de internet.

## 2. Caminho do executável

O Sinalo preenche automaticamente o caminho do perfil atual do Windows:

```text
%LocalAppData%\Programs\Louvor JA\Louvor JA.exe
```

Na máquina usada no desenvolvimento, isso corresponde a:

```text
C:\Users\fabri\AppData\Local\Programs\Louvor JA\Louvor JA.exe
```

**Não copie o nome fabri para outro computador.** Cada usuário tem seu próprio
perfil. Abra **Integrações > Louvor JA Flute** e confira o caminho preenchido.

Se o Louvor JA estiver em outra pasta:

1. Clique **Escolher executável…**.
2. Localize e selecione **Louvor JA.exe**, não a pasta ou o desinstalador.
3. Confira a porta do servidor e clique **Salvar caminho e porta**.
4. Se necessário, use **Restaurar caminho padrão** e salve novamente.

Você também pode editar o campo manualmente. O programa verifica o nome do
arquivo e sua existência ao salvar/abrir. A configuração é guardada neste perfil
do Sinalo e retomada na próxima execução. Isso não move a instalação do Louvor JA.

## 3. Preparar o Louvor JA

1. Clique **Abrir Louvor JA** no Sinalo, ou abra-o normalmente.
2. No Louvor JA, abra **Configurações > Transmissão (OBS / vMix)**.
3. Em interface de rede, selecione **Localhost (Apenas este computador)**.
4. Confira a porta. O padrão é **7071**.
5. Ative o servidor e habilite **Iniciar com o Louvor JA** para ele iniciar
   novamente quando o Flute for reaberto. Sem essa opção, poderá ser necessário
   reativá-lo manualmente a cada abertura. A opção do Sinalo não substitui essa
   configuração no Flute.
6. Configure os monitores de projeção **no Louvor JA** e confira o volume.

Não é necessário expor essa porta na internet ou configurar o roteador.
O Sinalo não ativa o servidor nem altera essas opções automaticamente.
O seletor de tela do Sinalo **não configura a saída do Louvor JA**.

## 4. Conectar e operar pelo Sinalo

1. Volte ao painel **Integrações > Louvor JA Flute** no Sinalo.
2. Use a mesma porta configurada no Louvor JA; salve se a alterou.
3. Clique **Abrir Louvor JA**: o Sinalo reutiliza o aplicativo aberto ou o inicia,
   aguarda até 30 segundos e conecta quando o servidor responder. Se ele já estava
   aberto ao iniciar o Sinalo, essa verificação ocorre automaticamente. Se precisar
   ativar o servidor depois desse prazo, clique **Tentar conectar novamente**.
4. Digite título, número do hino ou trecho de letra disponível no catálogo. A busca
   é automática após uma pausa de 350 ms na digitação; não precisa clicar em
   Pesquisar. Enter ou o botão Pesquisar consultam imediatamente. Você pode
   continuar digitando durante a consulta: somente o texto mais recente vale.
5. Selecione a música e o modo **Cantado**, **Instrumental** ou **Sem áudio**.
6. Clique **Abrir música no Louvor JA**. Essa ação pode iniciar áudio e projeção
   reais, conforme o modo e as configurações do Louvor JA.
7. Use **Reproduzir / pausar**, **Slide anterior**, **Próximo slide**,
   **Aplicar volume**, **Consultar estado** e **Encerrar música**.

Instrumental só é habilitado quando o catálogo informa que a música tem esse
recurso. O estado é acompanhado enquanto o painel está visível; ele não substitui
a conferência do áudio e da imagem no telão. Não repita cliques enquanto aguarda.

O catálogo mostra uma indicação durante a busca e avisa quando não há resultados.
Apagar o texto limpa resultados e seleção, sem consultar o catálogo inteiro.
Pesquisar nunca abre nem reproduz uma música automaticamente.

Abrir/conectar **não começa uma música**. Se o Louvor JA estiver fechado ao abrir
o Sinalo, ele não será iniciado sem sua ação. O Sinalo acompanha o fechamento mesmo
em outras telas e desabilita os controles automaticamente. Se você abrir outra
instância depois, a conexão será verificada novamente.

**Desconectar** ou **Cancelar espera** suspendem a reconexão automática até você
usar **Abrir Louvor JA** ou **Tentar conectar novamente**. Música já tocando não
é interrompida. Se o servidor parar, mas o aplicativo continuar aberto, a tela
informa comunicação perdida/servidor indisponível; confira-o e tente novamente.
Alterar caminho ou porta também exige nova conexão explícita.

## 5. Cuidados durante o culto

- **Antes de tocar uma música**, pare o vídeo e o áudio do cronômetro e feche
  a apresentação do Sinalo. O painel bloqueia abertura/retomada se os detectar.
- Antes de voltar a reproduzir vídeos ou ferramentas no Sinalo, **encerre a música
  no Louvor JA**. Os dois aplicativos ainda não têm arbitragem global de saídas.
- **Desconectar ou fechar o Sinalo não para a música nem fecha o Louvor JA.**
  Use **Encerrar música** enquanto a conexão estiver ativa.
- Cancelar a espera ou perder a conexão não desfaz comandos já recebidos pelo
  Louvor JA. Confira o aplicativo e o áudio antes de repetir um comando.
- Bíblia, liturgia, cronômetros, sorteio, fila e seleção de monitor não são
  controlados por esta primeira integração. Não confundir funções locais do
  Sinalo com o que pode ser comandado no Louvor JA.

## 6. Problemas comuns

| Situação | O que conferir |
| --- | --- |
| Louvor JA Flute não aparece | Atualize para Sinalo 1.6.0 ou superior, habilite-o em Configurações > Integrações e salve. |
| Executável não encontrado | Selecione Louvor JA.exe em sua pasta de instalação e salve. Não use o caminho de outro usuário do Windows. |
| Conexão recusada | Louvor JA aberto, servidor ativado e a mesma porta nos dois programas. |
| Aberto, mas servidor indisponível após 30 segundos | Ative o servidor local no Louvor JA e clique Tentar conectar novamente. |
| Não foi possível verificar o processo | Confira o caminho e execute ambos com o mesmo nível de permissão no Windows. |
| Desconectado pelo operador | Use Abrir Louvor JA ou Tentar conectar novamente para reativar a conexão nesta sessão. |
| Porta ocupada | Escolha uma porta livre no Louvor JA e use/salve a mesma no Sinalo. |
| Pesquisa vazia | Revise o termo/número e confira se o catálogo foi sincronizado no Louvor JA. |
| Música não inicia | Verifique se áudio/imagens estão baixados e se o Louvor JA apresentou uma mensagem ou confirmação. |
| Comando não confirmado | Confira o Louvor JA antes de repetir; receber a solicitação não garante sua execução. |
| Sem imagem ou na tela errada | Configure os monitores no Louvor JA; a escolha do Sinalo não é repassada a ele. |
| Sem som | Confira modo escolhido, volume da música, mixer do Windows e saída de áudio. |
| Conexão perdida durante a música | O áudio pode continuar. Abra o Louvor JA para operá-lo diretamente e depois reconecte. |

Não apague bancos de dados ou pastas de configuração para tentar corrigir a
conexão. O diagnóstico do painel pode ajudar; ele fica somente em memória.

## 7. Ensaio recomendado na igreja

Antes do culto, com tempo para conferir as saídas: conectar, pesquisar, tocar uma
música curta, pausar/continuar, mudar slides, aplicar volume e encerrar. Reabrir
o Sinalo e confirmar que caminho/porta foram mantidos. Deixe o Louvor JA acessível
como alternativa caso seja necessário operar diretamente por ele.

Confira também: iniciar Sinalo com Louvor JA aberto; abri-lo pelo botão quando
fechado; fechar/reabrir Louvor JA enquanto outra tela do Sinalo está selecionada;
desconectar manualmente e confirmar que ele não reconecta sozinho. Nenhum desses
passos deve iniciar música sem a escolha explícita do operador.
