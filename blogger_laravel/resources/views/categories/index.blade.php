@extends('layouts.app')

@section('content')
    <div class="d-flex justify-content-end">
        <a href="{{route('categories.create')}}" class="btn btn-success mb-2">Add Category</a>
    </div>
    <div class="card card-default">
        <div class="card-header">
            Categories
        </div>
        <div class="card-body">
            @if($categories->count()>0)
            <ul class="list-group">
                @foreach ($categories->all() as $category)
                <li class="list-group-item">
                    {{$category->name}}
                    <a href="{{route('categories.edit', $category->id)}}" class="btn btn-primary btn-sm float-right">
                        Edit
                    </a>
                    <button class="btn btn-danger btn-sm float-right mr-2" onclick="handleDelete({{$category->id}})">Delete</button>
                </li>
                @endforeach
            </ul>
            @else
            <h3 class="text-center">No results</h3>
            @endif
              <div class="modal fade" id="deleteModal" tabindex="-1" role="dialog" aria-labelledby="deleteModalLabel" aria-hidden="true">
                <div class="modal-dialog" role="document">
                  <form action="" method="POST" id="deleteCategoryForm">
                      @method('DELETE')
                      @csrf
                      <div class="modal-content">
                        <div class="modal-header">
                          <h5 class="modal-title" id="deleteModalLabel">Confirm delete</h5>
                          <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                            <span aria-hidden="true">&times;</span>
                          </button>
                        </div>
                        <div class="modal-body">
                            Are you sure you want to delete this category?
                        </div>
                        <div class="modal-footer">
                          <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancel</button>
                          <button type="submit" class="btn btn-danger">Delete</button>
                        </div>
                      </div>
                  </form>
                </div>
              </div>
        </div>
    </div>
@endsection

@section('scripts')
<script>
    function handleDelete(id){
        var form = document.getElementById("deleteCategoryForm")
        form.action = '/categories/'+id
        $('#deleteModal').modal('show')
    }
</script>
@endsection
