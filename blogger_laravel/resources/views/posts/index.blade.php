{{--                                                 VIEW TO SHOW ALL POSTS --}}

@extends('layouts.app')

@section('content')
    @if(!isset($trashed))
    <div class="d-flex justify-content-end">
        <a href="{{route('posts.create')}}" class="btn btn-success mb-2">Add Post</a>
    </div>
    @endif
    <div class="card card-default">
        <div class="card-header">
            {{isset($trashed)?'Trashed Posts':'Posts'}}
        </div>
        <div class="card-body">
            @if($posts->count()>0)
            <table class="table">
                <tbody>
                    @foreach($posts as $post)
                    <tr>
                        <td><img class="rounded" src="{{asset('storage/'.$post->image)}}" width="200px" alt=""></td>
                        <td>{{$post->title}}</td>
                        <td>{{$post->category->name}}</td>
                        <td>
                            <button href="" class="btn btn-sm btn-danger float-right" onclick="handleTrashDelete({{$post->id}})">
                                {{$post->trashed() ? 'Delete':'Trash'}}
                            </button>
                            @if($post->trashed())
                            <form action="{{route('posts.restore',$post->id)}}" method="POST" class="form-inline float-right">
                                @csrf
                                <button href="" class="btn btn-sm btn-warning mr-2" type="submit">
                                    Restore
                                </button>
                            </form>
                            @else
                            <a href="{{route('posts.edit',$post->id)}}" class="btn btn-sm btn-warning mr-2 float-right">Edit</a>
                            @endif
                        </td>
                    </tr>
                    @endforeach
                </tbody>
            </table>
            @else
            <h3 class="text-center">No results</h3>
            @endif
              <div class="modal fade" id="deleteModal" tabindex="-1" role="dialog" aria-labelledby="deleteModalLabel" aria-hidden="true">
                <div class="modal-dialog" role="document">
                  <form action="" method="POST" id="deletePostForm">
                      @method('DELETE')
                      @csrf
                      <div class="modal-content">
                        <div class="modal-header">
                          <h5 class="modal-title" id="deleteModalLabel">Confirm {{isset($trashed)?'Delete':'Trash'}}</h5>
                          <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                            <span aria-hidden="true">&times;</span>
                          </button>
                        </div>
                        <div class="modal-body">
                        {{isset($trashed)?'Permanently delete post?':'Trash this post? You can restore it later.'}}
                        </div>
                        <div class="modal-footer">
                          <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancel</button>
                          <button type="submit" class="btn btn-danger">{{isset($trashed)?'Delete':'Trash'}}</button>
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
    function handleTrashDelete(id){
        var form = document.getElementById("deletePostForm")
        form.action = '/posts/'+id
        $('#deleteModal').modal('show')
    }
</script>
@endsection
