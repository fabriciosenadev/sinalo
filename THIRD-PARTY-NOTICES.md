# Avisos de terceiros

Este arquivo registra dependências e conteúdos distribuídos com o Sinalo. A
licença do código do Sinalo não substitui nem altera as licenças abaixo.

| Componente | Uso no Sinalo | Aviso ou ação necessária |
| --- | --- | --- |
| .NET Runtime | Aplicativo e atualizador self-contained | Distribuir os avisos do runtime conforme a licença aplicável à versão publicada. |
| CommunityToolkit.Mvvm | Infraestrutura MVVM | Preservar a licença MIT distribuída pelo pacote NuGet. |
| Microsoft.Data.Sqlite | Banco de dados local | Preservar os avisos do pacote e de suas dependências. |
| SQLitePCLRaw.lib.e_sqlite3 | Biblioteca nativa SQLite | Preservar os avisos do pacote e da biblioteca SQLite. |
| MPV e bibliotecas nativas incluídas | Reprodução local de vídeo | O runtime está em `src/Sinalo.App/binaries/mpv`. Consultar e distribuir os avisos e obrigações aplicáveis a cada biblioteca. O `NOTICE.txt` presente nesse diretório ainda requer revisão antes de uma distribuição pública. |
| VLC e FFmpeg/ffprobe | Integração ou dependências externas opcionais | Não são licenciados pelo Sinalo. Caso sejam incorporados ao instalador em uma versão futura, incluir seus avisos, textos de licença e eventuais ofertas de código-fonte exigidos. |
| Vídeos, imagens, textos bíblicos e demais conteúdos | Conteúdo obtido de fontes oficiais ou configurado pelo operador | Permanecem sob os direitos e os termos de seus respectivos titulares. Não devem ser relicenciados como Apache-2.0. |

## Traduções bíblicas

Nenhuma tradução bíblica integral está coberta pela licença do Sinalo. Antes de
incluir um banco bíblico no instalador, deve haver registro da edição, origem,
licença aplicável, atribuição exigida e autorização expressa para busca,
projeção e distribuição offline quando necessária.

## Marca e identidade visual

O código-fonte pode ser reutilizado sob Apache-2.0. O nome **Sinalo**, os
ícones, o logotipo e a identidade visual não recebem uma licença de marca por
este arquivo ou pela licença do código. Derivações devem usar identidade
distinta, salvo autorização escrita.
