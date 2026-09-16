# Continuous integration

`tests.yml` runs the EditMode and PlayMode suites on every push to `main` and on
pull requests. The package has no Unity project of its own, so the workflow
builds a throwaway host project in `host/`, exports the checked-out commit into
its `Packages` folder, and lists the package under `testables` so its tests are
discovered. The checkout itself stays in the workspace root because the game-ci
CLI runs git commands there.

## Required secrets

Unity refuses to run in batch mode without a licence, so add these repository
secrets before the first run (**Settings → Secrets and variables → Actions**):

| Secret | Purpose |
|---|---|
| `UNITY_LICENSE` | Contents of the `.ulf` licence file for a Personal licence |
| `UNITY_EMAIL` | Unity account e-mail |
| `UNITY_PASSWORD` | Unity account password |

The licence file is produced by the activation flow described at
<https://game.ci/docs/github/activation>. `GITHUB_TOKEN` is provided by Actions
and needs no setup.

## Unity version

`UNITY_VERSION` at the top of the workflow pins the editor image. It is set to
the minimum version the package declares in `package.json`, so a green run means
the package still works on that minimum. Change both together.
