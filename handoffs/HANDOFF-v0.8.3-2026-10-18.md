# Handoff v0.8.3: OptiScaler 0.5.4, mochizuki com o modelo 310.9.16, proteção de pessoas, WrongGPU, 32-bit D3D11/OpenGL e add-on v0.7.13

18/10/2026. Tudo pronto e commitado em `master` (`421ae4e`, `e4809e6`, `774458a` e este handoff). **Nada foi enviado**:
nenhum push, tag, upload no HF ou release. A ordem de publicação está no fim.

## O que entrou

### Roteamento 32-bit (OptiScaler 0.5.4)

- `Work.OptiX86Wide` = 0.5.4 (`OptiRunsX86Wide`), ao lado do `OptiBridges` 0.5.2. `GraphicsDetection.OptiX86WideApis`
  = D3D11, D3D10, D3D9, OpenGL; `PresetFor/OptiApi/CanRunOptiScalerWith/RecommendedFor/NeedsRendererSwitchFor` ganharam
  `x86Wide`. Jogo 32-bit D3D11 ou OpenGL com 0.5.4 escolhida (ou a mais nova) -> OptiScaler; D3D8 32-bit continua no ReShade.
- Proxy: `x86\dxgi.dll` para D3D10/D3D11, `x86\opengl32.dll` para OpenGL, `x86\d3d9.dll` para D3D9 (`OptiX86Proxy`),
  ao lado do exe; OptiScaler, host e o resto em `OptiScaler64\` como nas rotas 32-bit de 0.5.2.
- `OptiScaler.ini` novo de um jogo 32-bit: `[DlssNr] Enabled=true` (`NrOn`, vale para todo jogo 32-bit agora),
  `PresentWithoutUpscaler=true` e `NrBackend` (mochizuki em RX 9000). Versão < 0.5.4 recusa 32-bit D3D11/GL com a
  mensagem que pede 0.5.4 (`X86Refused`).
- Log `opengl32-x86.log` no relatório e na limpeza do `OptiScaler64\` (o `dxgi-x86.log` já estava).

### Componentes novos no payload (só dentro de `releases.optiscaler[0.5.4]`)

| componente | versão | arquivo(s) | sha256 |
|---|---|---|---|
| `optiscaler` | 0.5.4-amd-nr | `OptiScaler-0.5.4-amd-nr.zip` (GitHub) | `be2846ef…95ca3e` |
| `lmxxf-gfx1200` | 0.5.4-amd-nr | kernels do lmxxf 0.43 | `1bd429fc…ad7f7` |
| `optiscaler-x86` | 0.5.4-amd-nr | 4 arquivos, com `opengl32.dll` | `e1fb4200…688da` |
| `lmxxf-weights` | 1 | o mesmo de antes | `7f6cdd1c…` |
| `mochizuki` | 0.5.4-amd-nr | 790 arquivos (101 shaders com `shaders\renodx\`, ACO, int4, prewarm de 33 pipelines) | `ba6ccba9…0723f` |
| `mochizuki-model` | 1 | `dlssnr.bin` (já no HF) | `2b41c888…7fbd` |
| `mochizuki-model-small` | 1 | `dlssnr-small.bin` solto, `mochizuki/model-small-1/` | `7e966e26…6e33` |
| `person-model` | 0.5.4-amd-nr | `person-model-0.5.4-amd-nr.zip`, 12 arquivos com as 4 licenças | `5ae1a050…720a` |
| `wronggpu` | 0.5.5-ea | 5 arquivos do WrongGPU + `WrongGPU-Setup-0.5.5-ea.exe`, soltos | setup `f7b167b5…6363` |
| `wronggpu-dlss` | 310.9.1 | `nvngx_dlss.dll` no endereço do GitHub da NVIDIA (`raw.githubusercontent.com/NVIDIA/DLSS/v310.9.1/...`) | `3975567b…3983` |
| `opti-runtime` | 0.6.0 | posto à mão (o script não copia), o mesmo da 0.5.3 | `195c4a89…5721` |

- Add-on e ponte no topo do payload: `0.7.13` (`addon/0.7.13/...`, `bridge/0.7.13/...`, espelhos no release v0.7.13).
  addon64 `bf7e8702…`, addon32 `f8fefa0d…`, host64 `f55a89d0…`, payload.sha256 `781706b5…`.
- O `person-model` zip **não** é o candidato de `opti\exports\release-0.5.4\hf\` (`2bc55970…`): foi refeito pelo
  `tools\pin-person-model.ps1` novo (determinístico) e é o que o payload pina. Subir o de
  `publish-person-model-0.5.4-amd-nr\`. Os outros zips deram os mesmos hashes dos candidatos.
- Instaladores antigos (v0.8.2) leem a 0.5.4 e instalam sem os componentes que não conhecem (sem modelo novo, sem
  pessoas, sem WrongGPU); o mochizuki 0.5.4 roda o 310.8 nesse caso.

### mochizuki 0.5.4 nas duas rotas

- `MochizukiComponents` = runtime + modelo + modelo novo; `Pins().MochizukiFiles` leva o `dlssnr-small.bin` quando o
  componente existe; `WithMochizuki()` (rota ReShade) carrega o modelo novo do mesmo release. A rota ReShade agora
  instala o mochizuki 0.5.4 inteiro (renodx, ACO, int4, os dois modelos) com o add-on/ponte 0.7.13, que mostra
  "Old model" quando os dois modelos estão lá.
- `IsMochizukiData` já aceitava `dlssnr-amd\shaders\renodx\` e `dlssnr-small.bin` (até 6 níveis); teste novo cobre.
- Desinstalação: além de `pipeline.cache`, `aco-cache`, `int4\pipeline.cache` e `int4\pipeline-binaries\`, agora
  `build.pending` e `build.failed`.
- `pin-mochizuki.ps1` aceita `dlssnr-amd\dlssnr-small.bin` e pina como `mochizuki-model-small` (`-SmallModelVersion`,
  mesma regra do modelo 1: mesmos bytes mantêm a versão, bytes novos pedem versão nova). Nenhum modelo vai no zip.

### Proteção de pessoas

- Componente `person-model`, pasta `person-model\` ao lado do OptiScaler (64-bit) ou dentro de `OptiScaler64\`
  (32-bit). Entra nos `OptiFiles` (vai com o OptiScaler), `Engine.OwnedFolders` ganhou `person-model`.
- Fica de fora com o lmxxf em placa que não é RDNA4 (`WithoutLmxxf`), e uma instalação dessas tira o que estava.
- Licenças na própria pasta (`onnxruntime_LICENSE.txt` MIT, `YuNet_MIT.txt`, `PPHumanSeg_Apache-2.0.txt`,
  `YOLO11_AGPL-3.0.txt`) e listadas no README (tabela de arquivos e Créditos). `pin-person-model.ps1` recusa uma
  pasta sem qualquer uma delas.
- `person-worker.log`/`.1.log` saem na desinstalação e entram no relatório.

### WrongGPU 0.5.5-ea (liberado pelo autor para esta versão)

- `Work.WrongGpu.cs`. Caixa **WrongGPU (DLSS 4.5 Model M, RX 9000)** no painel do OptiScaler (`GameSheet.WrongGpu.cs`),
  desligada por padrão, visível só com versão que traz o componente, placa RX 9000 e jogo 64-bit; escolha salva em
  `GameEntry.WrongGpu` (null segue a pasta, como o mochizuki). Strings novas nas 21 línguas.
- Instalação: antes da transação, roda o setup dele num `%TEMP%\amd-nr-wronggpu-<guid>\` com `Game.exe` falso (`MZ`),
  um pacote com `OptiScaler.dll` + `OptiScaler.ini` e o próprio exe copiado para lá (o caminho do staging passa de 260
  e o CreateProcess falhava): `/silent /dir=... /optiscaler=... /experimental`, depois `/silent /uninstall /dir=...`,
  e apaga a pasta. O modelo fica em `%LOCALAPPDATA%\WrongGPU\model\model.wgcm`. O DLSS da pasta temporária: o
  `wronggpu-dlss` baixado (só quando `Work.WrongGpuNeedsDlss`: sem modelo no PC e sem `nvngx_dlss.dll` 310.5+ no
  jogo) ou o do próprio jogo. Se o setup não gerar o modelo, nada é escrito e o relatório diz por quê.
- Arquivos no jogo: `wronggpu_ffx_dx12.dll`, `wronggpu_runtime.dll`, `plugins\WrongGPU.asi`, `wronggpu-license.txt`,
  `wronggpu-notices.txt`, `wronggpu.ini` próprio (`runtime_options=model=M;executor=dx12`; um que já existe e não é
  nosso fica) e `wronggpu-amd-compare.dll` = cópia do `OptiScaler\amd_fidelityfx_upscaler_dx12.dll` do pacote (é o que
  o setup faz; não hospedamos esse). Tudo no `Engine.Allowed`.
- Depois da transação, `OptiScaler.ini` (o nosso ou o do jogador) recebe só `[Libraries] FfxDx12SRPath=<caminho
  absoluto do wronggpu_ffx_dx12.dll>` e `[Plugins] LoadAsiPlugins=true`. Desmarcado ou desinstalado: os arquivos saem,
  os logs (`wronggpu-asi.log`, `wronggpu-fsr.log`, `wronggpu-diagnostics-*.jsonl`) e a pasta `plugins` vazia também, e
  as duas chaves voltam para `auto` se apontavam para o nosso.
- Como desligar numa versão futura: tirar o componente `wronggpu` (e `wronggpu-dlss`) do release no payload; a caixa
  some sozinha (`CarriesWrongGpu`).
- `tools\pin-wronggpu.ps1 -From <pasta onde o setup instalou> -Setup <exe> -SetupSha ... -Version 0.5.5-ea -Release
  0.5.4-amd-nr -Dlss <nvngx_dlss.dll 310.9.1>`; pinado a partir do Cyberpunk (`E:\Games\Cyberpunk2077\bin\x64`, só
  leitura) e do setup do scratchpad. Arquivos para upload em `publish-wronggpu-0.5.5-ea\`.

## Verificado

- `dotnet test`: 398 aprovados (novos: `Opti054Tests`, `WrongGpuTests`; `OptiScalerVersionTests` e `UserRuntimeTests`
  esperam 0.5.4).
- `tools\gate.ps1 -Ui`: build, test, line-limit, publish, ui, flows, 0 falhas.
- e2e (`scratchpad\rel054\e2e`, `AMDNR_HOME` temporário semeado de arquivos locais e do cache real só para leitura):
  OptiScaler com mochizuki em dx11, dx12+DLSS, vulkan, opengl, dx9, dx10, x86-dx9, x86-dx10, **x86-dx11**, **x86-gl**
  instalam (proxy certo, `Enabled=true` no 32-bit, modelo novo, 21 shaders renodx, 12 de person-model) e desinstalam
  deixando só o manifesto de configuração e o ini; x86-dx8 fica no ReShade. **dx12-wronggpu com o setup real**:
  modelo reaproveitado, arquivos e chaves certos, desmarcado tira tudo e volta as chaves para `auto`, desinstalação
  limpa; nenhuma entrada do WrongGPU sobrou no registro. **ReShade** dx11, dx12, x86-dx11 e x86-dx8 com add-on/ponte
  0.7.13 e mochizuki 0.5.4 (dois modelos, renodx, ACO, int4) instalam e desinstalam limpo.
- Testado à mão antes (18/10): o setup aceita um `Game.exe` de 2 bytes e um pacote só com dll + ini; sem modelo no PC
  ele converte do `nvngx_dlss.dll` posto na pasta ("this game"). O `nvngx_dlss.dll` do GitHub da NVIDIA é idêntico ao
  310.9.1 do Witcher 3.
- `check-release.ps1` falha agora só pelo que depende da publicação (a release mais nova ainda é a v0.8.2); tem que
  passar inteiro depois do passo 5.

## Artefatos

- Instalador: `opti\exports\release-0.5.4\installer\` (build de `774458a`): `AMD-NR-ReShade-Installer.exe`
  `6016f369ab11d189fc2b7e006b66d88c7b5d5c66813642ebb4604baefe926bef` (23711589 bytes), `SHA256SUMS.txt`,
  `AMD-NR-ReShade-Installer-v0.8.3.7z` (exe + `config.json`, `payload.json`, `api-db.json`). Notas da release:
  `opti\exports\release-0.5.4\notes-installer-0.8.3.md`. Changelog do Discord: `nr-bot\changelogs\2026-10-18_v0.8.3.md`.
- Para o HF: `publish-lmxxf-gfx1200-0.5.4-amd-nr\`, `publish-optiscaler-x86-0.5.4-amd-nr\`,
  `publish-mochizuki-0.5.4-amd-nr\`, `publish-person-model-0.5.4-amd-nr\`, `publish-wronggpu-0.5.5-ea\` (na raiz do
  instalador, ignorados pelo git) e `opti\exports\release-0.5.4\hf\dlssnr-small.bin`.

## Ordem de publicação (não executada)

Cada passo depende do anterior. Rodar do PowerShell.

1. **opti**: `opti\exports\release-0.5.4\PUBLICAR.md` passo 1 (push de `dlss-neural-rendering`, tag
   `v0.5.4-amd-nr` em `58dea8c94`, `gh release create` com o zip e `notes-opti-0.5.4.md`). Baixar o zip de volta e
   conferir `be2846ef3bed000755088bb378be203bc1fdc939c7a0a5357b1a8a4dac95ca3e`.
2. **add-on v0.7.13**: `reshade\release\v0.7.13\PUBLICAR.md` passos 3 e 4 (push, tag, release com os 6 arquivos).
3. **Hugging Face** (`zmodelerlover/amd-nr`, `--repo-type dataset`), de dentro de `AMD-NR-ReShade-Installer`:
   - `hf upload zmodelerlover/amd-nr publish-lmxxf-gfx1200-0.5.4-amd-nr\lmxxf-gfx1200-0.5.4-amd-nr.zip lmxxf-gfx1200/0.5.4-amd-nr/lmxxf-gfx1200-0.5.4-amd-nr.zip --repo-type dataset --commit-message "lmxxf RX 9060 kernels 0.5.4-amd-nr"`
   - `hf upload zmodelerlover/amd-nr publish-optiscaler-x86-0.5.4-amd-nr\optiscaler-x86-0.5.4-amd-nr.zip optiscaler-x86/0.5.4-amd-nr/optiscaler-x86-0.5.4-amd-nr.zip --repo-type dataset --commit-message "OptiScaler 32-bit proxies and host 0.5.4-amd-nr"`
   - `hf upload zmodelerlover/amd-nr publish-mochizuki-0.5.4-amd-nr\mochizuki-0.5.4-amd-nr.zip mochizuki/0.5.4-amd-nr/mochizuki-0.5.4-amd-nr.zip --repo-type dataset --commit-message "mochizuki 0.5.4-amd-nr: runtime, shaders, prewarm list and licence"`
   - `hf upload zmodelerlover/amd-nr ..\opti\exports\release-0.5.4\hf\dlssnr-small.bin mochizuki/model-small-1/dlssnr-small.bin --repo-type dataset --commit-message "mochizuki model-small 1 (310.9.16)"`
   - `hf upload zmodelerlover/amd-nr publish-person-model-0.5.4-amd-nr\person-model-0.5.4-amd-nr.zip person-model/0.5.4-amd-nr/person-model-0.5.4-amd-nr.zip --repo-type dataset --commit-message "person protection 0.5.4-amd-nr: worker, ONNX Runtime, models and licences"`
   - WrongGPU, os seis de `publish-wronggpu-0.5.5-ea\` em `wronggpu/0.5.5-ea/<nome>`: `wronggpu_ffx_dx12.dll`
     (`3511adb5…`), `wronggpu_runtime.dll` (`331bc00e…`), `WrongGPU.asi` (`e9aab52c…`), `wronggpu-license.txt`
     (`7a215435…`), `wronggpu-notices.txt` (`86671867…`), `WrongGPU-Setup-0.5.5-ea.exe` (`f7b167b5…`). Exemplo:
     `hf upload zmodelerlover/amd-nr publish-wronggpu-0.5.5-ea\wronggpu_ffx_dx12.dll wronggpu/0.5.5-ea/wronggpu_ffx_dx12.dll --repo-type dataset --commit-message "WrongGPU 0.5.5-ea: wronggpu_ffx_dx12.dll"`
     (os seis comandos exatos estão na saída do `pin-wronggpu.ps1`; `SHA256SUMS.txt` na mesma pasta).
   - O `nvngx_dlss.dll` **não** sobe: fica no endereço da NVIDIA. O `dlssnr.bin` (modelo 1) já está lá.
   - Conferir cada um de volta pelo hash (o passo 5 faz isso para todos).
4. **Instalador no GitHub**: `git push origin master`; `gh release create v0.8.3 installer\AMD-NR-ReShade-Installer.exe installer\SHA256SUMS.txt installer\AMD-NR-ReShade-Installer-v0.8.3.7z --repo zmodelerlover/AMD-NR-ReShade-Installer --title "v0.8.3" --notes-file notes-installer-0.8.3.md`
   (de dentro de `opti\exports\release-0.5.4`); `pwsh tools\check-release.ps1 -Exe publish-v0.8.3\AMD-NR-ReShade-Installer.exe` inteiro.
5. **Payload**: `.\tools\publish-payload.ps1 -Repo zmodelerlover/amd-nr -From E:\Projetos\opti_rr_dlss5\reshade\release\v0.7.13 -SetVersion addon=0.7.13 -SetVersion bridge=0.7.13`
   (sobe add-on/ponte em `addon/0.7.13/` e `bridge/0.7.13/`, o payload e o api-db, e baixa tudo de volta). Reverter a
   reescrita do `config.json`; commit "Publish the payload: OptiScaler 0.5.4-amd-nr, mochizuki 0.5.4, WrongGPU
   0.5.5-ea and add-on v0.7.13". Se o `payload.json` mudou, refazer o `.7z` com ele e `gh release upload v0.8.3 <7z>
   --clobber` (o atualizador não pina o 7z).
6. **AMD-NR-Extras**: copiar `payload\payload.json` (e `api-db.json` se mudou), commit e push; conferir que é igual ao do HF.
7. **Discord**: `python post_changelog.py changelogs\2026-10-18_v0.8.3.md --preview` (#admin), olhar, apagar o preview
   e postar; junto o changelog do opti 0.5.4 (`notes-opti-0.5.4.md`) e o do add-on v0.7.13. Threads da lista do
   `PUBLICAR.md` do opti (KCD2, FH6, os de 18/10), com menção a quem reportou.

## Em aberto

- O add-on v0.7.13 foi testado com o export `mochizuki-runtime-011`; a rota ReShade agora leva o runtime do release
  0.5.4 (`a3a35268…`). Mesma ABI (o e2e só instala, não roda): vale o teste no jogo do checklist do add-on.
- WrongGPU no jogo pelo instalador ainda não testado (só no Cyberpunk instalado à mão). O setup grava o próprio log em
  `%LOCALAPPDATA%\WrongGPU\setup.log` e reaproveita o modelo que já existe; com um modelo velho sem "output filter"
  ele procura um DLSS de novo: se não achar, a instalação falha com a mensagem do setup (o download da NVIDIA só
  acontece quando não há modelo nenhum).
- Proteção de pessoas em jogo 32-bit vai para `OptiScaler64\` como pede o `PUBLICAR.md` do opti, mas ela roda antes do
  upscaler e jogo 32-bit não tem upscaler: são 22 MB que provavelmente não servem lá.
- `Enabled=true` agora vale para todo `OptiScaler.ini` novo de jogo 32-bit, D3D9/D3D10 incluídos.
