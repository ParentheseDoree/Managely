<div>
	<h1 align="center">Managely</h1>
</div>

## Prérequis

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Bun](https://bun.sh/) (v1.3.6 ou supérieur)

## Installation

```bash
# Cloner le dépôt
git clone https://github.com/ParentheseDoree/Managely.git
cd Managely

# Installer les dépendances JavaScript
bun install

# Restaurer les dépendances .NET
dotnet restore

# Lancer les tests pour vérifier que tout fonctionne
dotnet test

# Compiler le projet
dotnet build
```

## Commandes

Tous les scripts sont centralisés dans le `package.json`.

| Commande                | Description                        |
| ----------------------- | ---------------------------------- |
| `bun run build`         | Compiler le projet                 |
| `bun run build:release` | Compiler en mode Release           |
| `bun run start`         | Lancer l'application               |
| `bun run test`          | Lancer les tests                   |
| `bun run commit`        | Créer un commit guidé (Commitizen) |

## Contribution

Pour contribuer au projet, consultez le [Guide de contribution](CONTRIBUTING.md).
