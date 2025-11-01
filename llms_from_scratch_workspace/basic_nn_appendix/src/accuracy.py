from basic_nn_appendix.src.types.network import NeuralNetwork
from torch.utils.data import DataLoader
import torch

def compute_accuracy(model: NeuralNetwork, data_loader: DataLoader):
    model = model.eval()
    correct = 0.0
    total_examples = 0.0

    for idx, (features, labels) in enumerate(data_loader):
        with torch.no_grad():
            logits = model(features)
        
        predictions = torch.argmax(logits, dim=1)
        compare = predictions == labels
        correct += torch.sum(compare)
        total_examples += len(compare)

    return (correct / total_examples).item()