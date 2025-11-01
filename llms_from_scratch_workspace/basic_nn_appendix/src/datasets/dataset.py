import torch

from basic_nn_appendix.src.types.dataset import ToyDataset

train_X = torch.tensor([[-1.2,3.1], [-0.5, 2.3], [-0.2, 1.9], [1.3, -3.2], [1.5, -3.5]])
train_y = torch.tensor([0, 0, 0, 1, 1])

test_X = torch.tensor([[-0.5, 2.0], [0.3, -1.5]])
test_y = torch.tensor([0, 1])

train_dataset = ToyDataset(train_X, train_y)
test_dataset = ToyDataset(test_X, test_y)