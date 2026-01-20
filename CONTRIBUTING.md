<p align="center">
	<h1>Guide de développement - Managely</h1>
</p>

## Comment contribuer

### 1. Fork et clone

* Fork le dépôt sur ton compte GitHub.
* Clone le fork sur ta machine locale :

```bash
git clone https://github.com/ParentheseDoree/Managely.git
cd Managely
```

### 2. Créer une branche

Crée une branche dédiée à ta fonctionnalité ou correction :

```bash
git checkout -b feature/ma-nouvelle-fonctionnalite
```

### 3. Développement

* Installe les dépendances et build le projet (voir la section Installation).
* Effectue tes modifications.
* Teste ton code avec `bun run test` ou `dotnet test`.

### 4. Commits guidés avec Commitizen

Avant de créer un commit avec Commitizen, **ajoute les fichiers modifiés à l’index Git** :

```bash
git add .
```

Puis lance Commitizen pour créer le commit guidé :

```bash
bun run commit
```

* Commitizen te guidera pour choisir le type de commit (feat, fix, chore, etc.) et rédiger un message clair.
* Exemple de type de commit :

  * `feat: ajouter un nouveau composant de fidélité`
  * `fix: correction du calcul des points de fidélité`

### 5. Push et Pull Request

* Pousse ta branche sur ton fork :

```bash
git push origin feature/ma-nouvelle-fonctionnalite
```

* Ouvre une **Pull Request** (PR) vers la branche `main` du dépôt original.
* Décris clairement ce que fait ta PR, les changements majeurs et toute information utile pour la relecture.

### 6. Revue et fusion

* Un mainteneur vérifiera ta PR.
* Après validation, ta PR sera fusionnée dans `main`.
