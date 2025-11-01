from torch.utils.data import Dataset

class ToyDataset(Dataset):
    def __init__(self, features, labels):
        self.features = features
        self.labels = labels
        
    def __len__(self):
        return len(self.labels)
    
    def __getitem__(self, idx):
        return self.features[idx], self.labels[idx]
    
    def to(self, device):
        self.features = self.features.to(device)
        self.labels = self.labels.to(device)
        return self
