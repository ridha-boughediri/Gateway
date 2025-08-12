# Intégration Azure Blob Storage - Messenger WhatsApp

## 🎯 Vue d'ensemble

Cette intégration ajoute le support complet d'Azure Blob Storage pour le stockage et la gestion des images dans votre messenger WhatsApp. Les utilisateurs peuvent maintenant uploader, stocker et envoyer des images via WhatsApp Business API.

## 📦 Fonctionnalités ajoutées

### ✅ Upload d'images
- Support des formats : JPEG, PNG, WebP
- Redimensionnement automatique (max 1920x1080)
- Génération de thumbnails (300x300)
- Compression optimisée (JPEG quality 85%)
- Limite de taille : 10MB par fichier

### ✅ Stockage sécurisé
- Stockage dans Azure Blob Storage
- Organisation par utilisateur (`userId/filename`)
- Métadonnées en base SQLite
- URLs sécurisées

### ✅ Intégration WhatsApp
- Envoi d'images via Twilio WhatsApp API
- Support des messages avec média
- Historique des conversations avec images

## 🔧 Configuration

### 1. Configuration Azure Storage

Mettez à jour votre `appsettings.json` :

```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=VOTRE_COMPTE;AccountKey=VOTRE_CLE;EndpointSuffix=core.windows.net",
    "ContainerName": "messenger-media",
    "BaseUrl": "https://VOTRE_COMPTE.blob.core.windows.net/"
  }
}
```

### 2. Création du compte Azure Storage

1. Créez un compte de stockage Azure
2. Récupérez la chaîne de connexion
3. Le conteneur sera créé automatiquement au premier démarrage

## 📡 Endpoints API

### Upload d'image
```http
POST /api/media/upload
Content-Type: multipart/form-data
Authorization: Bearer {token}

Body: file (IFormFile)
```

**Réponse :**
```json
{
  "id": 1,
  "fileName": "image.jpg",
  "blobUrl": "https://storage.blob.core.windows.net/messenger-media/1/guid.jpg",
  "thumbnailUrl": "https://storage.blob.core.windows.net/messenger-media/1/thumbnails/guid_thumb.jpg",
  "contentType": "image/jpeg",
  "fileSize": 245760,
  "width": 1920,
  "height": 1080,
  "uploadedAt": "2025-01-08T09:00:00Z"
}
```

### Envoi de message avec image
```http
POST /api/messages/send-with-media
Content-Type: application/json
Authorization: Bearer {token}

{
  "to": "+33612345678",
  "content": "Voici une image !",
  "mediaFileId": 1
}
```

### Récupération des médias utilisateur
```http
GET /api/media/user?page=1&pageSize=20
Authorization: Bearer {token}
```

### Téléchargement d'image
```http
GET /api/media/{id}/download
Authorization: Bearer {token}
```

### Suppression d'image
```http
DELETE /api/media/{id}
Authorization: Bearer {token}
```

## 🗄️ Base de données

### Nouvelle table : MediaFiles

```sql
CREATE TABLE "MediaFiles" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MediaFiles" PRIMARY KEY AUTOINCREMENT,
    "FileName" TEXT NOT NULL,
    "BlobUrl" TEXT NOT NULL,
    "ThumbnailUrl" TEXT NULL,
    "ContentType" TEXT NOT NULL,
    "FileSize" INTEGER NOT NULL,
    "Width" INTEGER NULL,
    "Height" INTEGER NULL,
    "UploadedAt" TEXT NOT NULL,
    "UserId" INTEGER NOT NULL,
    CONSTRAINT "FK_MediaFiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
```

### Table Messages mise à jour

Ajout du champ `MediaFileId` pour lier les messages aux fichiers média.

## 🔒 Sécurité

### Validation des fichiers
- Types MIME autorisés uniquement
- Limite de taille (10MB)
- Validation de l'extension de fichier
- Scan des dimensions d'image

### Permissions
- Chaque utilisateur ne peut accéder qu'à ses propres médias
- URLs Azure Blob sécurisées
- Authentification JWT requise

### Optimisations
- Compression automatique des images
- Génération de thumbnails pour les aperçus
- Redimensionnement intelligent

## 🚀 Utilisation

### 1. Upload d'une image
```javascript
const formData = new FormData();
formData.append('file', imageFile);

const response = await fetch('/api/media/upload', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`
  },
  body: formData
});

const mediaFile = await response.json();
```

### 2. Envoi via WhatsApp
```javascript
const messageData = {
  to: '+33612345678',
  content: 'Regardez cette image !',
  mediaFileId: mediaFile.id
};

await fetch('/api/messages/send-with-media', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`
  },
  body: JSON.stringify(messageData)
});
```

## 💰 Coûts Azure

### Estimation mensuelle (usage modéré)
- **Stockage** : 1GB → ~0.02€
- **Transactions** : 10,000 → ~0.004€
- **Bande passante** : 1GB sortie → ~0.08€
- **Total estimé** : ~0.10€/mois

### Optimisations de coût
- Compression automatique des images
- Suppression des fichiers inutilisés
- Lifecycle policies Azure (optionnel)

## 🔧 Maintenance

### Nettoyage automatique
Le système ne supprime pas automatiquement les anciens fichiers. Vous pouvez implémenter :

1. **Job de nettoyage** : Supprimer les médias non référencés
2. **Lifecycle policies Azure** : Archivage automatique après X jours
3. **Monitoring des coûts** : Alertes Azure

### Monitoring
- Logs détaillés des uploads/suppressions
- Métriques de performance
- Alertes en cas d'erreur Azure

## 🐛 Dépannage

### Erreurs communes

**"Azure Blob Service Client not initialized"**
- Vérifiez la chaîne de connexion Azure
- Assurez-vous que le compte de stockage existe

**"Type de fichier non supporté"**
- Seuls JPEG, PNG, WebP sont acceptés
- Vérifiez le Content-Type du fichier

**"Fichier trop volumineux"**
- Limite : 10MB par fichier
- Compressez l'image avant upload

### Logs utiles
```bash
# Voir les logs d'upload
dotnet run | grep "Image uploadée"

# Voir les erreurs Azure
dotnet run | grep "Erreur lors de"
```

## 🔄 Migration

Si vous avez des images existantes, vous pouvez les migrer vers Azure Blob Storage en utilisant l'endpoint d'upload et en mettant à jour les références dans la base de données.

---

## 📞 Support

Pour toute question sur l'intégration Azure Blob Storage, consultez :
- [Documentation Azure Blob Storage](https://docs.microsoft.com/azure/storage/blobs/)
- [Twilio WhatsApp API](https://www.twilio.com/docs/whatsapp)
